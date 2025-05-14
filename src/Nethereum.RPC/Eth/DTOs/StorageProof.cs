using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class StorageProof
    {
        /// <summary>
        /// QUANTITY - the requested storage key
        /// </summary>
        [JsonProperty(PropertyName = "key")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("key")]
#endif
        public HexBigInteger Key { get; set; }

        /// <summary>
        /// ARRAY - Array of rlp-serialized MerkleTree-Nodes, starting with the storageHash-Node, following the path of the SHA3 (key) as path.
        /// </summary>
        [JsonProperty(PropertyName = "proof")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("proof")]
#endif
        public List<string> Proof { get; set; }

        /// <summary>
        /// QUANTITY - the storage value
        /// </summary>
        [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
        public HexBigInteger Value { get; set; }
    }
}