using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{

    public class FillTransactionResponse
    {
        /// <summary>
        /// RLP-encoded bytes for the passed transaction object
        /// </summary>
        [JsonProperty(PropertyName = "raw")]
        public string Raw { get; set; }

        /// <summary>
        /// RLP-encoded bytes for the passed transaction object
        /// </summary>
        [JsonProperty(PropertyName = "raw")]
        public Transaction Transaction { get; set; }
    }

}
