using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class BlockOverrides
    {
        [JsonProperty(PropertyName = "baseFeePerGas", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("baseFeePerGas")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger BaseFeePerGas { get; set; }

        [JsonProperty(PropertyName = "blobBaseFee", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blobBaseFee")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger BlobBaseFee { get; set; }

        [JsonProperty(PropertyName = "feeRecipient", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("feeRecipient")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string FeeRecipient { get; set; }

        [JsonProperty(PropertyName = "gasLimit", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gasLimit")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger GasLimit { get; set; }

        [JsonProperty(PropertyName = "number", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("number")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger Number { get; set; }

        [JsonProperty(PropertyName = "prevRandao", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("prevRandao")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string PrevRandao { get; set; }

        [JsonProperty(PropertyName = "time", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("time")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger Time { get; set; }

        [JsonProperty(PropertyName = "withdrawals", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("withdrawals")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<Withdrawal> Withdrawals { get; set; }
    }
}
