using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Parity.RPC.Trace.TraceDTOs
{
    public class TraceFilterDTO
    {
        /// <summary>
        /// From this block
        /// </summary>
        [JsonProperty(PropertyName = "fromBlock")]
        public BlockParameter FromBlock { get; set; }
        /// <summary>
        /// To this block
        /// </summary>
        [JsonProperty(PropertyName = "toBlock")]
        public string ToBlock { get; set; }

        /// <summary>
        /// From address
        /// </summary>
        [JsonProperty(PropertyName = "fromAddress")]
        public string[] FromAddresses { get; set; }

        /// <summary>
        /// Count
        /// </summary>
        [JsonProperty(PropertyName = "count")]
        public int Count { get; set; }


        /// <summary>
        /// To address
        /// </summary>
        [JsonProperty(PropertyName = "toAddress")]
        public string[] ToAddress { get; set; }
    }
}
