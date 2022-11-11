using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class IstanbulSignersFromBlock
    {
        /// <summary>
        /// Retrieved block’s number
        /// </summary>
        [JsonProperty(PropertyName = "number")]
        public long Number { get; set; }

        /// <summary>
        /// Retrieved block’s hash
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        /// <summary>
        /// Address of the block proposer
        /// </summary>
        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        /// <summary>
        ///List of all addresses whose seal appears in this block
        /// </summary>
        [JsonProperty(PropertyName = "committers")]
        public string[] Committers { get; set; }
    }
}
