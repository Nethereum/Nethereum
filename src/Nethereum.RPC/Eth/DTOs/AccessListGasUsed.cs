using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccessListGasUsed
    {
        /// <summary>
        ///   Access list
        /// </summary>
        [JsonProperty(PropertyName = "accessList")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("accessList")]
#endif
        public List<AccessList> AccessList { get; set; }

        [JsonProperty(PropertyName = "error")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("error")]
#endif
        public string Error { get; set; }

        /// <summary>
        ///  Gas Used
        /// </summary>
        [JsonProperty(PropertyName = "gasUsed")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gasUsed")]
#endif
        public HexBigInteger GasUsed { get; set; }
    }
}