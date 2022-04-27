using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class NodeInfo
    {
        [JsonProperty(PropertyName =  "blockMakerAccount")]
        public string BlockMakerAccount { get; set; }

        [JsonProperty(PropertyName =  "voteAccount")]
        public string VoteAccount { get; set; }

        [JsonProperty(PropertyName =  "blockmakestrategy")]
        public BlockMakeStratregy BlockMakeStratregy { get; set; }

        [JsonProperty(PropertyName =  "canCreateBlocks")]
        public bool CanCreateBlocks { get; set; }

        [JsonProperty(PropertyName =  "canVote")]
        public bool CanVote { get; set; }
    }
}