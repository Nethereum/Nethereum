using System.Collections.Generic;
using System.Text;

namespace Nethereum.Generators.Net
{
    public class GeneratorSetConfig
    {
        public List<string> Paths { get; set; }
        public List<GeneratorConfig> GeneratorConfigs { get; set; }
    }

    public class GeneratorConfig
    {
        public string BaseNamespace { get; set; } = "";
        public string BasePath { get; set; }
        public int CodeGenLang { get; set; } = 0;
        public string GeneratorType { get; set; }
        public string MudNamespace { get; set; } = "";
    }
}
