using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class Authorisation
    {
        [JsonProperty(PropertyName = "chainId")]
        public HexBigInteger ChainId { get; set; }

        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

        [JsonProperty(PropertyName = "yParity")]
        public string YParity { get; set; }

        [JsonProperty(PropertyName = "r")]
        public string R { get; set; }

        [JsonProperty(PropertyName = "s")]
        public string S { get; set; }
    }
}