using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nethereum.Generators.Core;
using Newtonsoft.Json;

namespace Nethereum.Generator.Console
{
    public class GeneratorConfigurationFactory
    {
        public const string ConfigFileName = "Nethereum.Generator.json";

        private string ResolvePath(string filePath, string outputFolder)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            if (Path.IsPathRooted(filePath))
                return filePath;

            return Path.Combine(outputFolder, filePath);
        }

        public GeneratorConfiguration FromAbi(string contractName, string abiFilePath, string binFilePath, string baseNamespace, string outputFolder)
        {
            var config = new GeneratorConfiguration();

            binFilePath = ResolvePath(binFilePath, outputFolder);
            var byteCode = File.Exists(binFilePath) ? File.ReadAllText(binFilePath) : string.Empty;

            abiFilePath = ResolvePath(abiFilePath, outputFolder);
            var abi = File.Exists(abiFilePath) ? File.ReadAllText(abiFilePath) : string.Empty;

            if (string.IsNullOrEmpty(abi))
                throw new ArgumentException("Could not find abi file");

            if (string.IsNullOrEmpty(contractName))
                contractName = Path.GetFileNameWithoutExtension(abiFilePath);

            var abiConfig = new ABIConfiguration
            {
                ABI = abi,
                ByteCode = byteCode,
                ContractName = contractName
            };

            SetDefaults(abiConfig, baseNamespace, outputFolder);

            config.ABIConfigurations = new List<ABIConfiguration>();
            config.ABIConfigurations.Add(abiConfig);
            return config;
        }

        public GeneratorConfiguration FromProject(string destinationProjectFileName, string assemblyName)
        {
            CodeGenLanguage language = DeriveCodeGenLanguage(destinationProjectFileName);

            var fromAbiFiles = FromAbiFilesInProject(destinationProjectFileName, assemblyName, language);
            var fromConfigFile = FromConfigFile(destinationProjectFileName, assemblyName);

            if (fromConfigFile == null)
                return fromAbiFiles;

            return Merge(fromConfigFile, fromAbiFiles);
        }

        private CodeGenLanguage DeriveCodeGenLanguage(string destinationProjectFileName)
        {
            var extension = Path.GetExtension(destinationProjectFileName).ToLower();
            switch (extension)
            {
                case ".csproj":
                    return CodeGenLanguage.CSharp;
                case ".vbproj":
                    return CodeGenLanguage.Vb;
                case ".fsproj":
                    return CodeGenLanguage.FSharp;
                default:
                    throw new ArgumentException($"Could not derive code gen language. Unrecognised project file type ({extension}).");
            }
        }

        private static GeneratorConfiguration Merge(GeneratorConfiguration configFileGeneratorConfig, GeneratorConfiguration abiFileDrivenConfig)
        {
            //config file wins over abi based config
            foreach (var configFileDrivenAbi in configFileGeneratorConfig.ABIConfigurations)
            {
                var duplicateConfigEntries =
                    abiFileDrivenConfig.ABIConfigurations.Where(c =>
                        c.ContractName == configFileDrivenAbi.ContractName).ToArray();

                foreach (var duplicate in duplicateConfigEntries)
                {
                    abiFileDrivenConfig.ABIConfigurations.Remove(duplicate);
                }
            }

            abiFileDrivenConfig.ABIConfigurations.AddRange(configFileGeneratorConfig.ABIConfigurations);

            return abiFileDrivenConfig;
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

            var defaultNamespace = CreateNamespaceFromAssemblyName(assemblyName);

            SetDefaults(abiConfig, defaultNamespace, projectFolder);
            return abiConfig;
        }

        public GeneratorConfiguration FromConfigFile(string destinationProjectFileName, string assemblyName)
        {
            var projectFolder = Path.GetDirectoryName(destinationProjectFileName);
            var configFilePath = Path.Combine(projectFolder, ConfigFileName);

            if (!File.Exists(configFilePath))
                return null;

            var content = File.ReadAllText(configFilePath, Encoding.UTF8);
            var configuration = JsonConvert.DeserializeObject<GeneratorConfiguration>(content);

            if (configuration == null)
                return null;

            var defaultNamespace = CreateNamespaceFromAssemblyName(assemblyName);

            foreach (var abiConfiguration in configuration.ABIConfigurations)
            {
                SetDefaults(abiConfiguration, defaultNamespace, projectFolder);
            }

            return configuration;
        }

        private static string CreateNamespaceFromAssemblyName(string assemblyName)
        {
            return Path.GetFileNameWithoutExtension(assemblyName);
        }

        private static void SetDefaults(ABIConfiguration abiConfiguration, string defaultNamespace, string destinationProjectFolder)
        {
            if (string.IsNullOrEmpty(abiConfiguration.ABI))
                abiConfiguration.ABI = GetFileContent(destinationProjectFolder, abiConfiguration.ABIFile);

            if (string.IsNullOrEmpty(abiConfiguration.ByteCode)  && !string.IsNullOrEmpty(abiConfiguration.BinFile))
                abiConfiguration.ByteCode = GetFileContent(destinationProjectFolder, abiConfiguration.BinFile);

            if (string.IsNullOrEmpty(abiConfiguration.BaseOutputPath))
                abiConfiguration.BaseOutputPath = destinationProjectFolder;

            if (string.IsNullOrEmpty(abiConfiguration.BaseNamespace))
                abiConfiguration.BaseNamespace = defaultNamespace;

            if (string.IsNullOrEmpty(abiConfiguration.CQSNamespace))
                abiConfiguration.CQSNamespace = abiConfiguration.ContractName + ".CQS";

            if (string.IsNullOrEmpty(abiConfiguration.DTONamespace))
                abiConfiguration.DTONamespace = abiConfiguration.ContractName + ".DTO";

            if (string.IsNullOrEmpty(abiConfiguration.ServiceNamespace))
                abiConfiguration.ServiceNamespace = abiConfiguration.ContractName + ".Service";
        }

        private static string GetFileContent(string destinationProjectFolder, string pathToFile)
        {
            if(Path.IsPathRooted(pathToFile))
                return File.Exists(pathToFile) ? File.ReadAllText(pathToFile) : null;

            var projectPath = Path.Combine(destinationProjectFolder, pathToFile);
            if(File.Exists(projectPath))
                return File.ReadAllText(projectPath);

            var matchingFiles = Directory.GetFiles(destinationProjectFolder, pathToFile, SearchOption.AllDirectories);
            if(matchingFiles.Length > 0)
                return File.ReadAllText(matchingFiles.First());

            var fullPath = Path.GetFullPath(pathToFile);

            if(File.Exists(fullPath))
                return File.ReadAllText(fullPath);

            return null;
        }
    }
}