namespace Nethereum.Generators.Net
{

    using System;
    using System.Collections.Generic;
    using System.IO;
    using Nethereum.ABI.JsonDeserialisation;
    using Nethereum.Generators.Model;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class GeneratorSetProcessor
    {
        private readonly ContractGenerator _contractGenerator = new ContractGenerator();

        public List<string> GenerateFilesFromConfigJsonString(string configJson, string rootPath)
        {
            var configSets = JsonConvert.DeserializeObject<List<GeneratorSetConfig>>(configJson);
            return GenerateFilesFromConfigSets(configSets, rootPath);
        }

        public List<string> GenerateFilesFromConfigJsonFile(string configJsonPath, string rootPath)
        {
            var configJson = File.ReadAllText(configJsonPath);
            var configSets = JsonConvert.DeserializeObject<List<GeneratorSetConfig>>(configJson);
            return GenerateFilesFromConfigSets(configSets, rootPath);
        }

        private List<string> GenerateFilesFromConfigSets(List<GeneratorSetConfig> configSets, string rootPath)
        {
            var allGeneratedFiles = new List<string>();

            foreach (var configSet in configSets)
            {
                foreach (var relativePath in configSet.Paths)
                {
                    var absolutePath = Path.Combine(rootPath, relativePath);
                    allGeneratedFiles.AddRange(GenerateFilesUsingConfigSet(configSet, absolutePath, rootPath));
                }
            }

            return allGeneratedFiles;
        }

        private List<string> GenerateFilesUsingConfigSet(GeneratorSetConfig configSet, string filePath, string rootPath)
        {
            var generatedFiles = new List<string>();

            foreach (var config in configSet.GeneratorConfigs)
            {
                generatedFiles.AddRange(ProcessGeneratorConfig(config, filePath, rootPath));
            }

            return generatedFiles;
        }

        private string ExtractWordFromConfig(string configFilePath)
        {
            // Read the content of the file
            var configContent = File.ReadAllText(configFilePath);

            // Regex to match content inside the `defineWorld` function
            var regex = new System.Text.RegularExpressions.Regex(@"defineWorld\(([\s\S]*?)\);");
            var match = regex.Match(configContent);

            if (match.Success && match.Groups[1].Value is string worldConfigString)
            {
                worldConfigString = worldConfigString.Trim();

                // Parse the extracted content as JSON
                var worldConfig = JObject.Parse(worldConfigString);

                // Ensure the extracted content contains `tables` or `namespaces`
                if (worldConfig["tables"] != null || worldConfig["namespaces"] != null)
                {
                    // Convert the content to a JSON string
                    return worldConfig.ToString(Formatting.None);
                }
            }

            throw new InvalidOperationException("Unable to extract tables from config file.");
        }

        private List<string> ProcessGeneratorConfig(GeneratorConfig config, string filePath, string rootPath)
        {
            var files = new List<string>();

            // Resolve the absolute base path for the output
            var absolutePath = Path.Combine(rootPath, config.BasePath);

            // Extract ABI, bytecode, and contract name or JSON (depending on the generator type)
            ContractABI abi = null;
            string bytecode = null;
            string contractName = null;
            string json = null;

            if (!filePath.EndsWith("mud.config.ts", StringComparison.OrdinalIgnoreCase))
            {
                // Extract ABI, bytecode, and contract name for non-MUD Tables
                var extracted = _contractGenerator.ExtractAbiAndBytecode(filePath);
                abi = extracted.Abi;
                bytecode = extracted.Bytecode;
                contractName = extracted.ContractName;
            }
            else if (config.GeneratorType == "MudTables")
            {
                
                json = ExtractWordFromConfig(filePath);
            }

           
            switch (config.GeneratorType)
            {
                case "ContractDefinition":
                    files.AddRange(_contractGenerator.GenerateAllClassesInternal(
                        abi,
                        bytecode,
                        contractName,
                        config.BaseNamespace,
                        $"{contractName}",
                        $"{contractName}.ContractDefinition",
                        $"{contractName}.ContractDefinition",
                        config.SharedTypesNamespace,
                        config.SharedTypes,
                        absolutePath, 
                        Path.DirectorySeparatorChar.ToString(),
                        config.CodeGenLang
                    ));
                    break;

                case "UnityRequest":
                    files.AddRange(_contractGenerator.GenerateUnityRequests(
                        abi,
                        bytecode,
                        contractName,
                        config.BaseNamespace,
                        config.SharedTypesNamespace,
                        config.SharedTypes,
                        absolutePath, 
                        Path.DirectorySeparatorChar.ToString()
                    ));
                    break;

                case "MudExtendedService":
                    files.AddRange(_contractGenerator.GenerateMudService(
                        abi,
                        bytecode,
                        contractName,
                        config.BaseNamespace,
                        absolutePath,
                        config.SharedTypesNamespace,
                        config.SharedTypes,
                        Path.DirectorySeparatorChar.ToString(),
                        config.CodeGenLang,
                        config.MudNamespace
                    ));
                    break;

                case "MudTables":
                    files.AddRange(_contractGenerator.GenerateMudTables(
                        json,
                        config.BaseNamespace,
                        absolutePath, 
                        Path.DirectorySeparatorChar.ToString(),
                        config.CodeGenLang,
                        config.MudNamespace
                    ));
                    break;

                case "BlazorPageService":
                    files.Add(_contractGenerator.GenerateBlazorServicePage(
                        abi,
                        contractName,
                        config.BaseNamespace,
                        $"{contractName}",
                        $"{contractName}.ContractDefinition",
                        $"{contractName}.ContractDefinition",
                        config.SharedTypesNamespace,
                        absolutePath,
                        Path.DirectorySeparatorChar.ToString(),
                        config.CodeGenLang,
                        config.BlazorNamespace
                    ));
                    break;

                default:
                    throw new InvalidOperationException($"Unknown generator type: {config.GeneratorType}");
            }

            return files;
        }
    }

}
