using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class Sourcify4ByteResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("ok")]
#else
        [JsonProperty("ok")]
#endif
        public bool Ok { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("result")]
#else
        [JsonProperty("result")]
#endif
        public Sourcify4ByteResult Result { get; set; }
    }

    public class Sourcify4ByteResult
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("function")]
#else
        [JsonProperty("function")]
#endif
        public Dictionary<string, List<Sourcify4ByteSignatureInfo>> Function { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("event")]
#else
        [JsonProperty("event")]
#endif
        public Dictionary<string, List<Sourcify4ByteSignatureInfo>> Event { get; set; }
    }

    public class Sourcify4ByteSignatureInfo
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("filtered")]
#else
        [JsonProperty("filtered")]
#endif
        public bool Filtered { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("hasVerifiedContract")]
#else
        [JsonProperty("hasVerifiedContract")]
#endif
        public bool HasVerifiedContract { get; set; }
    }
}
