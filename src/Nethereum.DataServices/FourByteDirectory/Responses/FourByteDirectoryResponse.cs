using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization; 
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.FourByteDirectory.Responses
{
    public class FourByteDirectoryResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("count")]
#else
        [JsonProperty("count")]
#endif
        public int Count { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("next")]
#else
        [JsonProperty("next")]
#endif
        public string Next { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("previous")]
#else
        [JsonProperty("previous")]
#endif
        public string Previous { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("results")]
#else
        [JsonProperty("results")]
#endif
        public List<FourByteDirectorySignature> Signatures { get; set; }
    }
}
