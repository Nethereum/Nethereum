using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nethereum.Generator.Console.Configuration
{
    public class GeneratorConfiguration
    {
        public List<ABIConfiguration> ABIConfigurations { get; set; }

        public void SaveToJson(string outputDirectory, string fileName = null)
        {
            if (fileName == null)
                fileName = GeneratorConfigurationFactory.ConfigFileName;

            var fullPath = Path.Combine(outputDirectory, fileName);

            File.WriteAllText(fullPath, JsonConvert.SerializeObject(this), Encoding.UTF8);
        }
    }
}
