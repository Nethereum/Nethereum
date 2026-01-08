using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifySourceFile
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("content")]
#else
        [JsonProperty("content")]
#endif
        public string Content { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("encoding")]
#else
        [JsonProperty("encoding")]
#endif
        public string Encoding { get; set; }
    }
}
