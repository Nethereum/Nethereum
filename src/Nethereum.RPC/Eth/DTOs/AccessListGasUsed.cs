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
        public List<AccessList> AccessList { get; set; }

        [JsonProperty(PropertyName = "error")]
        public string Error { get; set; }

        /// <summary>
        ///  Gas Used
        /// </summary>
        [JsonProperty(PropertyName = "gasUsed")]
        public HexBigInteger GasUsed { get; set; }
    }
}