using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization; 
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyContentFile
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("path")]
#else
        [JsonProperty("path")]
#endif
        public string Path { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("content")]
#else
        [JsonProperty("content")]
#endif
        public string Content { get; set; }
    }
}
