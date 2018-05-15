using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Nethereum.ABI.Autogen.Configuration
{
    public class GeneratorConfiguration
    {
        public List<ABIConfiguration> ABIConfigurations { get; set; }

        public void SaveXml(string outputPath)
        {
            var serializer = new XmlSerializer(typeof(GeneratorConfiguration));
            using (var textWriter = File.Create(outputPath))
            {
                serializer.Serialize(textWriter, this);
            }
        }
    }
}
