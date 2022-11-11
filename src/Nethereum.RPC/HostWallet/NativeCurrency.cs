using Newtonsoft.Json;

namespace Nethereum.RPC.HostWallet
{
    public class NativeCurrency
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }

        [JsonProperty(PropertyName = "decimals")]
        public uint Decimals { get; set; }
    }
}