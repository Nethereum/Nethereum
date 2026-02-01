
using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.Generators;
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.Net.Mud;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.Generators.Net
{
    public class ContractGenerator
    {
        private readonly GeneratorModelABIDeserialiser _abiDeserialiser = new GeneratorModelABIDeserialiser();

        public (ContractABI Abi, string Bytecode, string ContractName) ExtractAbiAndBytecode(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            var contractName = Path.GetFileNameWithoutExtension(filePath);
            string rawAbi = null;
            string bytecode = "0x";

            if (extension == ".abi")
            {
                // Read ABI from file
                rawAbi = File.ReadAllText(filePath);

                // Look for corresponding .bin file for bytecode
                var binFile = Path.ChangeExtension(filePath, ".bin");
                if (File.Exists(binFile))
                {
                    bytecode = File.ReadAllText(binFile);
                }
            }
            else if (extension == ".json")
            {
                // Read and parse JSON content using JObject
                var content = File.ReadAllText(filePath);
                var compilationOutput = JObject.Parse(content);

                // Extract ABI if present
                if (compilationOutput["abi"] != null)
                {
                    rawAbi = compilationOutput["abi"].ToString(Formatting.None); // Keeps JSON as a single-line string
                }

                // Extract bytecode
                var bytecodeToken = compilationOutput["bytecode"];
                if (bytecodeToken != null)
                {
                    if (bytecodeToken.Type == JTokenType.String)
                    {
                        // Bytecode is a plain string
                        bytecode = bytecodeToken.ToString();
                    }
                    else if (bytecodeToken["object"] != null)
                    {
                        // Bytecode is nested inside an "object" property
                        bytecode = bytecodeToken["object"].ToString();
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"Unsupported file extension: {extension}");
            }

            if (string.IsNullOrEmpty(rawAbi))
            {
                throw new InvalidOperationException("ABI could not be extracted.");
            }

            // Deserialize ABI using Nethereum's deserializer
            var contractAbi = _abiDeserialiser.DeserialiseABI(rawAbi);
            return (contractAbi, bytecode, contractName);
        }

        public List<string> GenerateAllClassesInternal(
            ContractABI abi,
            string bytecode,
            string contractName,
            string baseNamespace,
            string serviceNamespace,
            string cqsNamespace,
            string dtoNamespace,
            string sharedTypesNamespace,
            string[] sharedGeneratedTypes,
            string basePath,
            string pathSeparator,
            int codeGenLang,
            string mudNamespace = null,
            string[] referencedTypesNamespaces = null,
            string[] structReferencedTypes = null)
        {
            var classGenerator = new ContractProjectGenerator(
                abi,
                contractName,
                bytecode,
                baseNamespace,
                serviceNamespace,
                cqsNamespace,
                dtoNamespace,
                sharedTypesNamespace,
                sharedGeneratedTypes,
                basePath,
                pathSeparator,
                (CodeGenLanguage)codeGenLang,
                referencedTypesNamespaces,
                structReferencedTypes
            )
            {
                AddRootNamespaceOnVbProjectsToImportStatements = false,
                MudNamespace = mudNamespace
            };

            var generatedClasses = classGenerator.GenerateAllMessagesFileAndService();
            return OutputGeneratedFiles(generatedClasses);
        }

        public List<string> GenerateUnityRequests(
            ContractABI abi,
            string bytecode,
            string contractName,
            string baseNamespace,
            string sharedTypesNamespace,
            string[] sharedGeneratedTypes,
            string basePath,
            string pathSeparator)
        {
            var generator = new ContractProjectGenerator(
                abi,
                contractName,
                bytecode,
                baseNamespace,
                contractName,
                $"{contractName}.ContractDefinition",
                $"{contractName}.ContractDefinition",
                sharedTypesNamespace,
                sharedGeneratedTypes,
                basePath,
                pathSeparator,
                0
            );

            var generatedFiles = generator.GenerateAllUnity();
            return OutputGeneratedFiles(generatedFiles);
        }

        public List<string> GenerateMudService(
            ContractABI abi,
            string bytecode,
            string contractName,
            string baseNamespace,
            string basePath,
            string sharedTypesNamespace,
            string[] sharedGeneratedTypes,
            string pathSeparator,
            int codeGenLang,
            string mudNamespace)
        {
            var generator = new ContractProjectGenerator(
                abi,
                contractName,
                bytecode,
                baseNamespace,
                contractName,
                $"{contractName}.ContractDefinition",
                $"{contractName}.ContractDefinition",
                sharedTypesNamespace,
                sharedGeneratedTypes,
                basePath,
                pathSeparator,
                (CodeGenLanguage)codeGenLang
            );

            var generatedFiles = generator.GenerateMudService(mudNamespace);
            return OutputGeneratedFiles(new[] { generatedFiles });
        }

        public List<string> GenerateMudTables(
            string json,
            string baseNamespace,
            string basePath,
            string pathSeparator,
            int codeGenLang,
            string mudNamespace)
        {
            var tables = MudWorldParser.ExtractTables(json);

            tables = tables.FindAll(t =>
                t.MudNamespace == mudNamespace ||
                (string.IsNullOrEmpty(mudNamespace) &&
                 string.IsNullOrEmpty(t.MudNamespace)));

            var generator = new MudTablesGenerator(tables.ToArray(), baseNamespace, (CodeGenLanguage)codeGenLang, basePath, pathSeparator, mudNamespace);
            var generatedFiles = generator.GenerateAllTables();
            return OutputGeneratedFiles(generatedFiles);
        }

        public string GenerateBlazorServicePage(ContractABI abi,
            string contractName,
            string baseNamespace,
            string serviceNamespace,
            string cqsNamespace,
            string dtoNamespace,
            string sharedTypesNamespace,
            string basePath,
            string pathSeparator,
            int codeGenLang,
            string blazorNamespace
            )
        {
            var blazorGenerator = new BlazorPagesGenerator(
                abi,
                contractName,
                baseNamespace,
                serviceNamespace,
                cqsNamespace,
                dtoNamespace,
                sharedTypesNamespace,
                CodeGenLanguage.CSharp,
                basePath,
                pathSeparator,
                blazorNamespace

            );

            var generatedFile = blazorGenerator.GenerateFile();
            var outputFiles = OutputGeneratedFiles(new[] { generatedFile });
            return outputFiles[0];
        }

        private List<string> OutputGeneratedFiles(IEnumerable<GeneratedFile> generatedFiles)
        {
            var outputFiles = new List<string>();

            foreach (var file in generatedFiles)
            {
                Directory.CreateDirectory(file.OutputFolder);
                var fullPath = Path.Combine(file.OutputFolder, file.FileName);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }

                File.WriteAllText(fullPath, file.GeneratedCode);
                outputFiles.Add(fullPath);
            }

            return outputFiles;
        }
    }
}
