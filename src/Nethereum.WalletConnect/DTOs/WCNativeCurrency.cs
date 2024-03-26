using Newtonsoft.Json;

namespace Nethereum.WalletConnect.DTOs
{
    public class WCNativeCurrency
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }

        [JsonProperty(PropertyName = "decimals")]
        public string Decimals { get; set; }
    }
}





