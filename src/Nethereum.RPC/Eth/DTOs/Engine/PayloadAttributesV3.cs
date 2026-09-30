using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class PayloadAttributesV3 : PayloadAttributesV2
    {
        [JsonProperty(PropertyName = "parentBeaconBlockRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("parentBeaconBlockRoot")]
#endif
        public string ParentBeaconBlockRoot { get; set; }
    }
}
