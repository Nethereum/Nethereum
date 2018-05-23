using Nethereum.Generators.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.Generator.Console.Configuration
{
    /// <summary>
    /// Responsible from retrieving code generator configuration and setting default values
    /// </summary>
    public class GeneratorConfigurationFactory : IGeneratorConfigurationFactory
    {
        public GeneratorConfiguration FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder)
        {
            var config = new GeneratorConfiguration();

            var abi = GeneratorConfigurationUtils.GetFileContent(outputFolder, abiFilePath);

            if (string.IsNullOrEmpty(abi))
                throw new ArgumentException("Could not find abi file or abi content is empty");

            if (string.IsNullOrEmpty(binFilePath))
                binFilePath = abiFilePath.Replace(".abi", ".bin");

            var byteCode = GeneratorConfigurationUtils.GetFileContent(outputFolder, binFilePath);

            if (string.IsNullOrEmpty(contractName))
                contractName = Path.GetFileNameWithoutExtension(abiFilePath);

            var abiConfig = new ABIConfiguration
            {
                ABI = abi,
                ByteCode = byteCode,
                ContractName = contractName
            };

            abiConfig.ResolveEmptyValuesWithDefaults(baseNamespace, outputFolder);

            config.ABIConfigurations = new List<ABIConfiguration>();
            config.ABIConfigurations.Add(abiConfig);
            return config;
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

            if (string.IsNullOrEmpty(projectFilePath) ||
                !File.Exists(projectFilePath))
                return null;

            var language = GeneratorConfigurationUtils.DeriveCodeGenLanguage(projectFilePath);

            return FromAbiFilesInProject(projectFilePath, assemblyName, language);
        }

        public GeneratorConfiguration FromAbiFilesInProject(string projectFileName, string assemblyName, CodeGenLanguage language)
        {
            var config = new GeneratorConfiguration();
            var projectFolder = Path.GetDirectoryName(projectFileName);
            var projectName = Path.GetFileNameWithoutExtension(projectFileName);
            var abiFiles = Directory.GetFiles(projectFolder, "*.abi", SearchOption.AllDirectories);
            var abiConfigurations = new List<ABIConfiguration>(abiFiles.Length);
            config.ABIConfigurations = abiConfigurations;

            foreach (var abiFile in abiFiles)
            {
                var abiConfig = CreateAbiConfiguration(abiFile, projectFolder, projectName, assemblyName, language);
                abiConfigurations.Add(abiConfig);
            }

            return config;
        }

        private static ABIConfiguration CreateAbiConfiguration(string abiFile, string projectFolder, string projectName, string assemblyName, CodeGenLanguage language)
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

            var configuration = GeneratorConfiguration.FromJson(configFilePath);

            if (configuration == null)
                return null;

            var defaultNamespace = GeneratorConfigurationUtils.CreateNamespaceFromAssemblyName(assemblyName);

            foreach (var abiConfiguration in configuration.ABIConfigurations)
            {
                abiConfiguration.ResolveEmptyValuesWithDefaults(defaultNamespace, destinationProjectFolder);
            }

            return configuration;
        }





    }
}