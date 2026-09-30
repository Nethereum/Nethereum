using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.TxPool.DTOs
{
    public class TxPoolContentResponse
    {
        [JsonProperty(PropertyName = "pending", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("pending")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public Dictionary<string, Dictionary<string, PendingTransactionInfo>> Pending { get; set; }

        [JsonProperty(PropertyName = "queued", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("queued")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public Dictionary<string, Dictionary<string, PendingTransactionInfo>> Queued { get; set; }
    }
}
