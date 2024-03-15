using Newtonsoft.Json;

namespace Nethereum.WalletConnect.DTOs
{
    public class WCAddEthereumChainParameter
    {
        [JsonProperty(PropertyName = "chainId")]
        public string ChainId { get; set; }

        [JsonProperty(PropertyName = "blockExplorerUrls", NullValueHandling = NullValueHandling.Ignore)]
        public string[] BlockExplorerUrls { get; set; }
        
        [JsonProperty(PropertyName = "chainName")]
        public string ChainName { get; set; }

        [JsonProperty(PropertyName = "iconUrls", NullValueHandling = NullValueHandling.Ignore)]
        public string[] IconUrls { get; set; }

        [JsonProperty(PropertyName = "nativeCurrency", NullValueHandling = NullValueHandling.Ignore)]
        public WCNativeCurrency NativeCurrency { get; set; }

        [JsonProperty(PropertyName = "rpcUrls", NullValueHandling = NullValueHandling.Ignore)]
        public string[] RpcUrls { get; set; }
    }
}





