using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class BlockMakeStratregy

    {
        [DataMember(Name =  "maxblocktime")]
        public int MaxBlockTime { get; set; }

        [DataMember(Name =  "minblocktime")]
        public int MinBlockTime { get; set; }

        [DataMember(Name =  "status")]
        public string Status { get; set; }

        [DataMember(Name =  "type")]
        public string Type { get; set; }
    }
}