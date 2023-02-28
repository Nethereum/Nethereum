using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.HostWallet
{
    public class SwitchEthereumChainParameter
    {
        [JsonProperty(PropertyName = "chainId")]
        public HexBigInteger ChainId { get; set; }
    }
}