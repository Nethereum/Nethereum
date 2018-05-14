using System.IO;
using System.Xml.Serialization;

namespace Nethereum.Generators.Nuget.Console.Configuration
{
    public class GeneratorConfigurationFactory
    {
        public GeneratorConfiguration ReadFromProjectPath(string destinationProjectFileName)
        {
            var projectFolder = Path.GetDirectoryName(destinationProjectFileName);
            var configFile = Path.Combine(projectFolder, "Nethereum.Generator.config");
            return CreateFromXmlConfig(Path.GetFileName(destinationProjectFileName), projectFolder, configFile);
        }

        public GeneratorConfiguration CreateFromXmlConfig(string destinationProjectName, string destinationProjectFolder, string configFilePath)
        {
            GeneratorConfiguration configuration = null;

            var serializer = new XmlSerializer(typeof(GeneratorConfiguration));
            using (var fileReader = File.OpenRead(configFilePath))
            {
                configuration = (GeneratorConfiguration)serializer.Deserialize(fileReader);
            }

            if (configuration == null)
                return null;

            var defaultNamespace = Path.GetFileNameWithoutExtension(destinationProjectName);

            foreach (var abiConfiguration in configuration.ABIConfigurations)
            {
                SetDefaults(destinationProjectFolder, defaultNamespace, abiConfiguration);
            }

            return configuration;
        }

        private static void SetDefaults(string destinationProjectFolder, string defaultNamespace, ABIConfiguration abiConfiguration)
        {
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