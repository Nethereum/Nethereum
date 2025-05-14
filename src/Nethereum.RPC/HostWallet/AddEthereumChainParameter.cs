using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.HostWallet
{
    public class AddEthereumChainParameter
    {
        [JsonProperty(PropertyName = "chainId")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("chainId")]
#endif
        public HexBigInteger ChainId { get; set; }
        [JsonProperty(PropertyName = "blockExplorerUrls")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockExplorerUrls")]
#endif
        public List<string> BlockExplorerUrls { get; set; }
        [JsonProperty(PropertyName = "chainName")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("chainName")]
#endif
        public string ChainName { get; set; }
        [JsonProperty(PropertyName = "iconUrls")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("iconUrls")]
#endif
        public List<string> IconUrls { get; set; }
        [JsonProperty(PropertyName = "nativeCurrency")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nativeCurrency")]
#endif
        public NativeCurrency NativeCurrency { get; set; }
        [JsonProperty(PropertyName = "rpcUrls")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("rpcUrls")]
#endif
        public List<string> RpcUrls { get; set; }
    }
}