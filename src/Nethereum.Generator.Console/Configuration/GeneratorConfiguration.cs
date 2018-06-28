using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Converters;

namespace Nethereum.Generator.Console.Configuration
{
    public class GeneratorConfiguration
    {
        private static readonly JsonConverter _jsonConverter = new StringEnumConverter();

        public List<ABIConfiguration> ABIConfigurations { get; set; }

        public void SaveToJson(string outputDirectory, string fileName = null)
        {
            if (fileName == null)
                fileName = GeneratorConfigurationUtils.ConfigFileName;

            var fullPath = Path.Combine(outputDirectory, fileName);

            File.WriteAllText(fullPath, JsonConvert.SerializeObject(this, _jsonConverter), Encoding.UTF8);
        }

        public static GeneratorConfiguration FromJson(string jsonFile)
        {
            var content = File.ReadAllText(jsonFile, Encoding.UTF8);
            var configuration = JsonConvert.DeserializeObject<GeneratorConfiguration>(content, _jsonConverter);
            return configuration;
        }
    }
}
