using System;


#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanGetAccountInternalTransactionsResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("blockNumber")]
#else
        [JsonProperty("blockNumber")]
#endif
        public string BlockNumber { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("timeStamp")]
#else
        [JsonProperty("timeStamp")]
#endif
        public string TimeStamp { get; set; }

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
        [JsonPropertyName("value")]
#else
        [JsonProperty("value")]
#endif
        public string Value { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("contractAddress")]
#else
        [JsonProperty("contractAddress")]
#endif
        public string ContractAddress { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("input")]
#else
        [JsonProperty("input")]
#endif
        public string Input { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("type")]
#else
        [JsonProperty("type")]
#endif
        public string Type { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gas")]
#else
        [JsonProperty("gas")]
#endif
        public string Gas { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gasUsed")]
#else
        [JsonProperty("gasUsed")]
#endif
        public string GasUsed { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("traceId")]
#else
        [JsonProperty("traceId")]
#endif
        public string TraceId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("isError")]
#else
        [JsonProperty("isError")]
#endif
        public string IsError { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("errCode")]
#else
        [JsonProperty("errCode")]
#endif
        public string ErrCode { get; set; }
    }
}
