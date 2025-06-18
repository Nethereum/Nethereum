using Newtonsoft.Json;

namespace Nethereum.RPC.HostWallet
{
    public class WatchAssetParametersOptions
    {
        /// <summary>
        /// The hexadecimal Ethereum address of the token contract
        /// </summary>
        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }
        /// <summary>
        /// A ticker symbol or shorthand, up to 5 alphanumerical characters
        /// </summary>
        [JsonProperty(PropertyName = "symbol")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("symbol")]
#endif
        public string Symbol { get; set; }
        /// <summary>
        /// The number of asset decimals
        /// </summary>
        [JsonProperty(PropertyName = "decimals")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("decimals")]
#endif
        public uint Decimals { get; set; }
        /// <summary>
        /// A string url of the token logo
        /// </summary>
        [JsonProperty(PropertyName = "image")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("image")]
#endif
        public string Image { get; set; }

        /// <summary>
        /// The unique identifier of the NFT (required for ERC-721 and ERC-1155 tokens).
        /// </summary>
        [JsonProperty(PropertyName = "tokenId")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("tokenId")]
#endif
        public string TokenId { get; set; }
    
}


}
