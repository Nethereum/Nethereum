using System.Runtime.Serialization;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Parity.RPC.Trace.TraceDTOs
{
    public class TraceFilterDTO
    {
        /// <summary>
        ///     From this block
        /// </summary>
        [DataMember(Name =  "fromBlock")]
        public BlockParameter FromBlock { get; set; }

        /// <summary>
        ///     To this block
        /// </summary>
        [DataMember(Name =  "toBlock")]
        public BlockParameter ToBlock { get; set; }

        /// <summary>
        ///     From address
        /// </summary>
        [DataMember(Name =  "fromAddress")]
        public string[] FromAddresses { get; set; }

        /// <summary>
        ///     Count
        /// </summary>
        [DataMember(Name =  "count")]
        public int Count { get; set; }


        /// <summary>
        ///    After (optional) The offset trace number
        /// </summary>
        [DataMember(Name =  "after")]
        public int After { get; set; }

        /// <summary>
        ///     To address
        /// </summary>
        [DataMember(Name =  "toAddress")]
        public string[] ToAddress { get; set; }
    }
}