using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos
{
    public class AccountStorageValue
    {
        [JsonProperty(PropertyName = "key")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("key")]
#endif
        public HexBigInteger Key { get; set; }

        [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
        public string Value { get; set; }
    }
}