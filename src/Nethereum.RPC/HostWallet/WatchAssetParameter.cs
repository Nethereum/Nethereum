using Newtonsoft.Json;

namespace Nethereum.RPC.HostWallet
{
    public class WatchAssetParameter
    {
        /// <summary>
        /// The asset's interface, e.g. 'ERC20'
        /// </summary>
        [JsonProperty(PropertyName = "type")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("type")]
#endif
        public string Type { get; set; } = "ERC20";

        [JsonProperty(PropertyName = "options")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("options")]
#endif
        public WatchAssetParametersOptions Options { get; set; } = new WatchAssetParametersOptions();
    }
}
