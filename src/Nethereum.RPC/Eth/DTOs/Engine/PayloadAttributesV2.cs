using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class PayloadAttributesV2 : PayloadAttributesV1
    {
        [JsonProperty(PropertyName = "withdrawals")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("withdrawals")]
#endif
        public List<Withdrawal> Withdrawals { get; set; } = new List<Withdrawal>();
    }
}
