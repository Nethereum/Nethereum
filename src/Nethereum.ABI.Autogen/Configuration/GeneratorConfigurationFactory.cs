using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Nethereum.Generators.Core;

namespace Nethereum.ABI.Autogen.Configuration
{
    public class GeneratorConfigurationFactory
    {
        public const string ConfigFileName = "Nethereum.ABI.Autogen.config";

        public GeneratorConfiguration FromProject(string destinationProjectFileName, string assemblyName)
        {
            CodeGenLanguage language = DeriveCodeGenLanguage(destinationProjectFileName);

            var fromAbiFiles = FromAbiFiles(destinationProjectFileName, assemblyName, language);
            var fromConfigFile = FromConfigFile(destinationProjectFileName, assemblyName);

            if (fromConfigFile == null)
                return fromAbiFiles;

            return Merge(fromConfigFile, fromAbiFiles);
        }

        private CodeGenLanguage DeriveCodeGenLanguage(string destinationProjectFileName)
        {
            var extension = Path.GetExtension(destinationProjectFileName);
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

        public GeneratorConfiguration FromAbiFiles(string projectFileName, string assemblyName, CodeGenLanguage language)
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

            SetDefaults(projectFolder, projectName, assemblyName, abiConfig);
            return abiConfig;
        }

        public GeneratorConfiguration FromConfigFile(string destinationProjectFileName, string assemblyName)
        {
            var projectFolder = Path.GetDirectoryName(destinationProjectFileName);
            var configFilePath = Path.Combine(projectFolder, ConfigFileName);

            if (!File.Exists(configFilePath))
                return null;

            GeneratorConfiguration configuration = null;

            var serializer = new XmlSerializer(typeof(GeneratorConfiguration));
            using (var fileReader = File.OpenRead(configFilePath))
            {
                configuration = (GeneratorConfiguration)serializer.Deserialize(fileReader);
            }

            if (configuration == null)
                return null;

            foreach (var abiConfiguration in configuration.ABIConfigurations)
            {
                SetDefaults(projectFolder, destinationProjectFileName, assemblyName, abiConfiguration);
            }

            return configuration;
        }

        private static void SetDefaults(string destinationProjectFolder, string projectName, string assemblyName, ABIConfiguration abiConfiguration)
        {
            var defaultNamespace = Path.GetFileNameWithoutExtension(assemblyName);

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
    }
}