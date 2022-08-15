using Newtonsoft.Json;

namespace Nethereum.RPC.HostWallet
{
    public class WatchAssetParameter
    {
        /// <summary>
        /// The asset's interface, e.g. 'ERC20'
        /// </summary>
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; } = "ERC20";

        [JsonProperty(PropertyName = "options")]
        public WatchAssetParametersOptions Options { get; set; } = new WatchAssetParametersOptions();
    }
}
