using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Nethereum.Generator.Console
{
    public class GeneratorConfiguration
    {
        public List<ABIConfiguration> ABIConfigurations { get; set; }

        public void SaveToJson(string outputPath)
        {
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(this), Encoding.UTF8);
        }
    }
}
