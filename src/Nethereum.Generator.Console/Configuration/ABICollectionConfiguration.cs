using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nethereum.Generators.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Nethereum.Generator.Console.Configuration
{
    public class ABICollectionConfiguration
    {
        public string BaseNamespace { get; set; }

        public CodeGenLanguage CodeGenLanguage { get; set; }

        public string BaseOutputPath { get; set; }

        public List<ABIConfiguration> ABIConfigurations { get; set; }

        public Models.ProjectGenerator GetGeneratorConfiguration()
        {
            return new Models.ProjectGenerator
            {
                Namespace = BaseNamespace,
                OutputFolder = BaseOutputPath,
                Language = CodeGenLanguage,
                Contracts = ABIConfigurations.Select(x => x.GetContractDefinition(BaseOutputPath)).ToList()
            };
        }

        private static readonly JsonConverter JsonConverter = new StringEnumConverter();

        public void SaveToJson(string outputDirectory, string fileName = null)
        {
            if (fileName == null)
                fileName = GeneratorConfigurationUtils.ConfigFileName;

            var fullPath = Path.Combine(outputDirectory, fileName);

            File.WriteAllText(fullPath, JsonConvert.SerializeObject(this, JsonConverter), Encoding.UTF8);
        }

        public static ABICollectionConfiguration FromJson(string jsonFile, string defaultNamespace)
        {
            var content = File.ReadAllText(jsonFile, Encoding.UTF8);
            var configuration = JsonConvert.DeserializeObject<ABICollectionConfiguration>(content, JsonConverter);
            configuration.BaseNamespace = defaultNamespace;
            return configuration;
        }
    }
}
