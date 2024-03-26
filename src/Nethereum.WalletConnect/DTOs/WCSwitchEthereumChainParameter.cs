using Newtonsoft.Json;

namespace Nethereum.WalletConnect.DTOs
{
    public class WCSwitchEthereumChainParameter
    {
        [JsonProperty(PropertyName = "chainId")]
        public string ChainId { get; set; }
    }
}





