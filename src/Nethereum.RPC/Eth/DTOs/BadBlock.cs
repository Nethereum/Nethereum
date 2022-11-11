using Newtonsoft.Json;
namespace Nethereum.RPC.Eth.DTOs
{
    public class BadBlock
    {
        /// <summary>
        ///     DATA, 32 Bytes - hash of the block.  
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        public string Hash { get; set; }

        [JsonProperty(PropertyName = "rlp")]
        public string Rlp { get; set; }

        [JsonProperty(PropertyName = "block")]
        public string Block { get; set; }
    }
}

