using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccountProof
    {
        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }
        /// <summary>
        /// QUANTITY - the balance of the account. See eth_getBalance
        /// </summary>
        [JsonProperty(PropertyName = "balance")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("balance")]
#endif
        public HexBigInteger Balance { get; set; }
        /// <summary>
        /// DATA, 32 Bytes - hash of the code of the account. For a simple Account without code it will return "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470"
        /// </summary>
        [JsonProperty(PropertyName = "codeHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("codeHash")]
#endif
        public string CodeHash { get; set; }

        /// <summary>
        /// QUANTITY, - nonce of the account. See eth_getTransactionCount
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nonce")]
#endif
        public HexBigInteger Nonce { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - SHA3 of the StorageRoot. All storage will deliver a MerkleProof starting with this rootHash.
        /// </summary>
        [JsonProperty(PropertyName = "storageHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("storageHash")]
#endif
        public string StorageHash { get; set; }

        /// <summary>
        /// ARRAY - Array of rlp-serialized MerkleTree-Nodes, starting with the stateRoot-Node, following the path of the SHA3 (address) as key.
        /// </summary>
        [JsonProperty(PropertyName = "accountProof")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("accountProof")]
#endif
        public List<string> AccountProofs { get; set; }


        /// <summary>
        /// Array of storage-entries as requested.
        /// </summary>
        [JsonProperty(PropertyName = "storageProof")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("storageProof")]
#endif
        public List<StorageProof> StorageProof { get; set; }

    }
}