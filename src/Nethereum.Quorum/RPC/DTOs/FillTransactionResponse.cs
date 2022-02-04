using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class FillTransactionResponse
    {
        /// <summary>
        /// RLP-encoded bytes for the passed transaction object
        /// </summary>
        [DataMember(Name = "raw")]
        public string Raw { get; set; }

        /// <summary>
        /// RLP-encoded bytes for the passed transaction object
        /// </summary>
        [DataMember(Name = "raw")]
        public Transaction Transaction { get; set; }
    }

}
