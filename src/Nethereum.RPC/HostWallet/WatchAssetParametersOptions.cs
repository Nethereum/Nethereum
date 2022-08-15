using Newtonsoft.Json;

namespace Nethereum.RPC.HostWallet
{
    public class WatchAssetParametersOptions
    {
        /// <summary>
        /// The hexadecimal Ethereum address of the token contract
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }
        /// <summary>
        /// A ticker symbol or shorthand, up to 5 alphanumerical characters
        /// </summary>
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        /// <summary>
        /// The number of asset decimals
        /// </summary>
        [JsonProperty(PropertyName = "decimals")]
        public uint Decimals { get; set; }
        /// <summary>
        /// A string url of the token logo
        /// </summary>
        [JsonProperty(PropertyName = "image")]
        public string Image { get; set; }
    }
}
