using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanResponse<T>
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("status")]
#else
        [JsonProperty("status")]
#endif
        public string Status { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("message")]
#else
        [JsonProperty("message")]
#endif
        public string Message { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("result")]
#else
        [JsonProperty("result")]
#endif
        public T Result { get; set; }
    }
}
