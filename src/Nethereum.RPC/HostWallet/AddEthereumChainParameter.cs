using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.HostWallet
{
    public class AddEthereumChainParameter
    {
        [JsonProperty(PropertyName = "chainId")]
        public string ChainId { get; set; }
        [JsonProperty(PropertyName = "blockExplorerUrls")]
        public List<string> BlockExplorerUrls { get; set; }
        [JsonProperty(PropertyName = "chainName")]
        public string ChainName { get; set; }
        [JsonProperty(PropertyName = "iconUrls")]
        public List<string> IconUrls { get; set; }
        [JsonProperty(PropertyName = "nativeCurrency")]
        public NativeCurrency NativeCurrency { get; set; }
        [JsonProperty(PropertyName = "rpcUrls")]
        public List<string> RpcUrls { get; set; }
    }
}