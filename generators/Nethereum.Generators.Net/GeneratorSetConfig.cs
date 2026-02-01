using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Nethereum.Generators.Net
{
    public class GeneratorSetConfig
    {
        [JsonProperty("paths")]
        public List<string> Paths { get; set; }
        [JsonProperty("generatorConfigs")]
        public List<GeneratorConfig> GeneratorConfigs { get; set; }
    }

    public class GeneratorConfig
    {
        [JsonProperty("baseNamespace")]
        public string BaseNamespace { get; set; } = "";
        [JsonProperty("basePath")]
        public string BasePath { get; set; }
        [JsonProperty("codeGenLang")]
        public int CodeGenLang { get; set; } = 0;
        [JsonProperty("generatorType")]
        public string GeneratorType { get; set; }
        [JsonProperty("mudNamespace")]
        public string MudNamespace { get; set; } = "";
        [JsonProperty("sharedTypesNamespace")]
        public string SharedTypesNamespace { get; set; } = "";
        [JsonProperty("sharedTypes")]
        public string[] SharedTypes { get; set; } = null;
        [JsonProperty("blazorNamespace")]
        public string BlazorNamespace { get; set; } = "";
        [JsonProperty("referencedTypesNamespaces")]
        public string[] ReferencedTypesNamespaces { get; set; } = null;
        [JsonProperty("structReferencedTypes")]
        public string[] StructReferencedTypes { get; set; } = null;
    }
}
