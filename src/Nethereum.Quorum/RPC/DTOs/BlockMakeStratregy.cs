using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{

    public class BlockMakeStratregy

    {
        [JsonProperty(PropertyName =  "maxblocktime")]
        public int MaxBlockTime { get; set; }

        [JsonProperty(PropertyName =  "minblocktime")]
        public int MinBlockTime { get; set; }

        [JsonProperty(PropertyName =  "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName =  "type")]
        public string Type { get; set; }
    }
}