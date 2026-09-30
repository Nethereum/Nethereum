using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class ExecutionPayloadV1
    {
        [JsonProperty(PropertyName = "parentHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("parentHash")]
#endif
        public string ParentHash { get; set; }

        [JsonProperty(PropertyName = "feeRecipient")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("feeRecipient")]
#endif
        public string FeeRecipient { get; set; }

        [JsonProperty(PropertyName = "stateRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("stateRoot")]
#endif
        public string StateRoot { get; set; }

        [JsonProperty(PropertyName = "receiptsRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("receiptsRoot")]
#endif
        public string ReceiptsRoot { get; set; }

        [JsonProperty(PropertyName = "logsBloom")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("logsBloom")]
#endif
        public string LogsBloom { get; set; }

        [JsonProperty(PropertyName = "prevRandao")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("prevRandao")]
#endif
        public string PrevRandao { get; set; }

        [JsonProperty(PropertyName = "blockNumber")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockNumber")]
#endif
        public HexBigInteger BlockNumber { get; set; }

        [JsonProperty(PropertyName = "gasLimit")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("gasLimit")]
#endif
        public HexBigInteger GasLimit { get; set; }

        [JsonProperty(PropertyName = "gasUsed")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("gasUsed")]
#endif
        public HexBigInteger GasUsed { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
#endif
        public HexBigInteger Timestamp { get; set; }

        [JsonProperty(PropertyName = "extraData")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("extraData")]
#endif
        public string ExtraData { get; set; }

        [JsonProperty(PropertyName = "baseFeePerGas")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("baseFeePerGas")]
#endif
        public HexBigInteger BaseFeePerGas { get; set; }

        [JsonProperty(PropertyName = "blockHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockHash")]
#endif
        public string BlockHash { get; set; }

        [JsonProperty(PropertyName = "transactions")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("transactions")]
#endif
        public List<string> Transactions { get; set; } = new List<string>();
    }
}
