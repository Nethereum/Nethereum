using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class NodeInfo
    {
        [DataMember(Name =  "blockMakerAccount")]
        public string BlockMakerAccount { get; set; }

        [DataMember(Name =  "voteAccount")]
        public string VoteAccount { get; set; }

        [DataMember(Name =  "blockmakestrategy")]
        public BlockMakeStratregy BlockMakeStratregy { get; set; }

        [DataMember(Name =  "canCreateBlocks")]
        public bool CanCreateBlocks { get; set; }

        [DataMember(Name =  "canVote")]
        public bool CanVote { get; set; }
    }
}