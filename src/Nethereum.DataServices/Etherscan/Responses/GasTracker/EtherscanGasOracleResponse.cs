#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.GasTracker
{
    public class EtherscanGasOracleResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("LastBlock")]
#else
        [JsonProperty("LastBlock")]
#endif
        public string LastBlock { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("SafeGasPrice")]
#else
        [JsonProperty("SafeGasPrice")]
#endif
        public string SafeGasPrice { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("ProposeGasPrice")]
#else
        [JsonProperty("ProposeGasPrice")]
#endif
        public string ProposeGasPrice { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("FastGasPrice")]
#else
        [JsonProperty("FastGasPrice")]
#endif
        public string FastGasPrice { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("suggestBaseFee")]
#else
        [JsonProperty("suggestBaseFee")]
#endif
        public string SuggestBaseFee { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gasUsedRatio")]
#else
        [JsonProperty("gasUsedRatio")]
#endif
        public string GasUsedRatio { get; set; }
    }
}