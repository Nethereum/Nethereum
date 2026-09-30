using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class EthSimulateInput
    {
        [JsonProperty(PropertyName = "blockStateCalls")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockStateCalls")]
#endif
        public List<BlockStateCall> BlockStateCalls { get; set; } = new List<BlockStateCall>();

        [JsonProperty(PropertyName = "returnFullTransactions", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("returnFullTransactions")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public bool? ReturnFullTransactions { get; set; }

        [JsonProperty(PropertyName = "traceTransfers", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("traceTransfers")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public bool? TraceTransfers { get; set; }

        [JsonProperty(PropertyName = "validation", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("validation")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public bool? Validation { get; set; }
    }

    public class BlockStateCall
    {
        [JsonProperty(PropertyName = "blockOverrides", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockOverrides")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public BlockOverrides BlockOverrides { get; set; }

        [JsonProperty(PropertyName = "stateOverrides", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("stateOverrides")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public Dictionary<string, AccountOverride> StateOverrides { get; set; }

        [JsonProperty(PropertyName = "calls", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("calls")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<TransactionInput> Calls { get; set; }
    }
}
