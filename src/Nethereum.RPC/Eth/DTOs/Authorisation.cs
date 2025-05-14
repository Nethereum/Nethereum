using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class Authorisation
    {
        [JsonProperty(PropertyName = "chainId")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("chainId")]
#endif
        public HexBigInteger ChainId { get; set; }

        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }

        [JsonProperty(PropertyName = "nonce")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nonce")]
#endif
        public HexBigInteger Nonce { get; set; }

        [JsonProperty(PropertyName = "yParity")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("yParity")]
#endif
        public string YParity { get; set; }

        [JsonProperty(PropertyName = "r")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("r")]
#endif
        public string R { get; set; }

        [JsonProperty(PropertyName = "s")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("s")]
#endif
        public string S { get; set; }
    }
}