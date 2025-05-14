using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.HostWallet
{
    public class SwitchEthereumChainParameter
    {
        [JsonProperty(PropertyName = "chainId")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("chainId")]
#endif
        public HexBigInteger ChainId { get; set; }
    }
}