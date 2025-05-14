using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos
{
    public class DebugStorageAtResult
    {
        [JsonProperty(PropertyName = "storage")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("storage")]
#endif
        public Dictionary<string, AccountStorageValue> Storage { get; set; } = new Dictionary<string, AccountStorageValue>();

        [JsonProperty(PropertyName = "nextKey")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nextKey")]
#endif
        public string NextKey { get; set; }
    }
}