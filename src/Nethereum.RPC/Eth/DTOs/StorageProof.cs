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
        public HexBigInteger Key { get; set; }

        /// <summary>
        /// ARRAY - Array of rlp-serialized MerkleTree-Nodes, starting with the storageHash-Node, following the path of the SHA3 (key) as path.
        /// </summary>
        [JsonProperty(PropertyName = "proof")]
        public List<string> Proof { get; set; }

        /// <summary>
        /// QUANTITY - the storage value
        /// </summary>
        [JsonProperty(PropertyName = "value")]
        public HexBigInteger Value { get; set; }
    }
}