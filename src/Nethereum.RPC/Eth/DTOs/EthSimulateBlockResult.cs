using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class EthSimulateBlockResult : Block
    {
        [JsonProperty(PropertyName = "calls")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("calls")]
#endif
        public List<EthSimulateCallResult> Calls { get; set; } = new List<EthSimulateCallResult>();

        [JsonProperty(PropertyName = "transactions")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactions")]
#endif
        public object[] Transactions { get; set; } = new object[0];
    }
}
