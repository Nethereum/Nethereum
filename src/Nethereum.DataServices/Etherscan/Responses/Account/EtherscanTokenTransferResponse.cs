using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanTokenTransferResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("blockNumber")]
#else
        [JsonProperty("blockNumber")]
#endif
        public string BlockNumber { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("hash")]
#else
        [JsonProperty("hash")]
#endif
        public string Hash { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("from")]
#else
        [JsonProperty("from")]
#endif
        public string From { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("to")]
#else
        [JsonProperty("to")]
#endif
        public string To { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("contractAddress")]
#else
        [JsonProperty("contractAddress")]
#endif
        public string ContractAddress { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("value")]
#else
        [JsonProperty("value")]
#endif
        public string Value { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("tokenName")]
#else
        [JsonProperty("tokenName")]
#endif
        public string TokenName { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("tokenSymbol")]
#else
        [JsonProperty("tokenSymbol")]
#endif
        public string TokenSymbol { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("tokenDecimal")]
#else
        [JsonProperty("tokenDecimal")]
#endif
        public string TokenDecimal { get; set; }
    }
}