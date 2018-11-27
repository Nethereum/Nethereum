using Nethereum.Generators.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Nethereum.Generator.Console.Configuration
{
    /// <summary>
    /// Responsible from retrieving code generator configuration and setting default values
    /// </summary>
    public class GeneratorConfigurationFactory : IGeneratorConfigurationFactory
    {
        public GeneratorConfiguration FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder)
        {
            var abi = GeneratorConfigurationUtils.GetFileContent(outputFolder, abiFilePath);

            if (string.IsNullOrEmpty(abi))
                throw new ArgumentException("Could not find abi file or abi content is empty");

            if (string.IsNullOrEmpty(binFilePath))
                binFilePath = abiFilePath.Replace(".abi", ".bin");

            var byteCode = GeneratorConfigurationUtils.GetFileContent(outputFolder, binFilePath);

            if (string.IsNullOrEmpty(contractName))
                contractName = Path.GetFileNameWithoutExtension(abiFilePath);

            return new GeneratorConfiguration
            {
                Namespace = baseNamespace,
                OutputFolder = outputFolder,
                Contracts = new List<CompiledContract>
                {
                    new CompiledContract
                    {
                        ContractName = contractName,
                        AbiString = abi,
                        Bytecode = byteCode
                    }
                }
            };
        }

        public GeneratorConfiguration FromProject(string destinationProjectFolderOrFileName, string assemblyName)
        {
            (string projectFolder, string projectFilePath) =
                GeneratorConfigurationUtils.GetFullFileAndFolderPaths(destinationProjectFolderOrFileName);

            if (string.IsNullOrEmpty(projectFolder) ||
                !Directory.Exists(projectFolder))
                return null;

            var codeGenConfig = FromConfigFile(projectFolder, assemblyName);
            if (codeGenConfig != null)
                return codeGenConfig;

            if (string.IsNullOrEmpty(projectFilePath))
                projectFilePath = GeneratorConfigurationUtils.FindFirstProjectFile(projectFolder);

            if (string.IsNullOrEmpty(projectFilePath) ||
                !File.Exists(projectFilePath))
                return null;

            var language = GeneratorConfigurationUtils.DeriveCodeGenLanguage(projectFilePath);

            return FromAbiFilesInProject(projectFilePath, assemblyName, language);
        }

        public GeneratorConfiguration FromAbiFilesInProject(string projectFileName, string assemblyName, CodeGenLanguage language)
        {
            var projectFolder = Path.GetDirectoryName(projectFileName);
            var abiFiles = Directory.GetFiles(projectFolder, "*.abi", SearchOption.AllDirectories);
            var contracts = new List<CompiledContract>(abiFiles.Length);

            foreach (var file in abiFiles)
            {
                var contractName = Path.GetFileNameWithoutExtension(file);
                var binFileName = Path.Combine(Path.GetDirectoryName(file), contractName + ".bin");
                var byteCode = File.Exists(binFileName) ? File.ReadAllText(binFileName) : string.Empty;

                contracts.Add(new CompiledContract
                {
                    ContractName = contractName,
                    AbiString = File.ReadAllText(file),
                    Bytecode = byteCode
                });
            }

            return new GeneratorConfiguration
            {
                Language = language,
                Contracts = contracts,
                Namespace = GeneratorConfigurationUtils.CreateNamespaceFromAssemblyName(assemblyName)
            };
        }

        public GeneratorConfiguration FromCompiledContractDirectory(string directory, string outputFolder, string baseNamespace, CodeGenLanguage language)
        {
            var directoryName = Path.GetDirectoryName(directory);
            var compiledContracts = Directory.GetFiles(directoryName, "*.json", SearchOption.AllDirectories);
            var contracts = new List<CompiledContract>(compiledContracts.Length);

            foreach (var file in compiledContracts)
            {
                var contract = JsonConvert.DeserializeObject<CompiledContract>(File.ReadAllText(file));
                contracts.Add(contract);
            }

            return new GeneratorConfiguration
            {
                Language = language,
                Contracts = contracts,
                OutputFolder = outputFolder,
                Namespace = baseNamespace
            };
        }

        private static ABIConfiguration CreateAbiConfiguration(string abiFile, string projectFolder, string assemblyName, CodeGenLanguage language)
        {
            var contractName = Path.GetFileNameWithoutExtension(abiFile);

            var binFileName = Path.Combine(Path.GetDirectoryName(abiFile), contractName + ".bin");
            var byteCode = File.Exists(binFileName) ? File.ReadAllText(binFileName) : string.Empty;

            var abiConfig = new ABIConfiguration
            {
                CodeGenLanguage = language,
                ABI = File.ReadAllText(abiFile),
                ByteCode = byteCode,
                ContractName = contractName
            };

            var defaultNamespace = GeneratorConfigurationUtils.CreateNamespaceFromAssemblyName(assemblyName);
            abiConfig.ResolveEmptyValuesWithDefaults(defaultNamespace, projectFolder);
            return abiConfig;
        }


        public GeneratorConfiguration FromConfigFile(string destinationProjectFolder, string assemblyName)
        {
            var configFilePath = GeneratorConfigurationUtils.DeriveConfigFilePath(destinationProjectFolder);

            if (!File.Exists(configFilePath))
                return null;

            var defaultNamespace = GeneratorConfigurationUtils.CreateNamespaceFromAssemblyName(assemblyName);
            var configuration = GeneratorConfiguration.FromJson(configFilePath, defaultNamespace);

            return configuration;
        }
    }
}