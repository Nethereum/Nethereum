using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class IstanbulSignersFromBlock
    {
        /// <summary>
        /// Retrieved block’s number
        /// </summary>
        [DataMember(Name = "number")]
        public long Number { get; set; }

        /// <summary>
        /// Retrieved block’s hash
        /// </summary>
        [DataMember(Name = "hash")]
        public string Hash { get; set; }

        /// <summary>
        /// Address of the block proposer
        /// </summary>
        [DataMember(Name = "author")]
        public string Author { get; set; }

        /// <summary>
        ///List of all addresses whose seal appears in this block
        /// </summary>
        [DataMember(Name = "committers")]
        public string[] Committers { get; set; }
    }
}
