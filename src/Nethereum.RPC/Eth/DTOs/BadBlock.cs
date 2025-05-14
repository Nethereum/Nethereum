using Newtonsoft.Json;
namespace Nethereum.RPC.Eth.DTOs
{
    public class BadBlock
    {
        /// <summary>
        ///     DATA, 32 Bytes - hash of the block.  
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("hash")]
#endif
        public string Hash { get; set; }

        [JsonProperty(PropertyName = "rlp")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("rlp")]
#endif
        public string Rlp { get; set; }

        [JsonProperty(PropertyName = "block")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("block")]
#endif
        public string Block { get; set; }
    }
}

