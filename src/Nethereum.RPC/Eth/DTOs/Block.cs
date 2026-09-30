using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System.Collections;
using System.Runtime.Serialization;
namespace Nethereum.RPC.Eth.DTOs
{


    public class Block
    {
        /// <summary>
        ///     QUANTITY - the block number. null when its pending block. 
        /// </summary>
        [JsonProperty(PropertyName = "number")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("number")]
#endif
        public HexBigInteger Number { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block.  
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("hash")]
#endif
        public string BlockHash { get; set; }

        /// <summary>
        ///  block author. Parity-only; not part of the standard eth_getBlockBy* shape — omitted
        ///  from output when unset.
        /// </summary>
        [JsonProperty(PropertyName = "author", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("author")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string Author { get; set; }


        /// <summary>
        ///  Seal fields. Parity-only; not part of the standard eth_getBlockBy* shape — omitted from
        ///  output when unset.
        /// </summary>
      [JsonProperty(PropertyName = "sealFields", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("sealFields")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string[] SealFields { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the parent block. 
        /// </summary>
      [JsonProperty(PropertyName = "parentHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("parentHash")]
#endif
        public string ParentHash { get; set; }

        /// <summary>
        ///     DATA, 8 Bytes - hash of the generated proof-of-work. null when its pending block. 
        /// </summary>
      [JsonProperty(PropertyName = "nonce")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nonce")]
#endif
        public string Nonce { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - SHA3 of the uncles data in the block. 
        /// </summary>
      [JsonProperty(PropertyName = "sha3Uncles")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("sha3Uncles")]
#endif
        public string Sha3Uncles { get; set; }


        /// <summary>
        ///     DATA, 256 Bytes - the bloom filter for the logs of the block. null when its pending block. 
        /// </summary>
      [JsonProperty(PropertyName = "logsBloom")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("logsBloom")]
#endif
        public string LogsBloom { get; set; }


        /// <summary>
        ///     DATA, 32 Bytes - the root of the transaction trie of the block.
        /// </summary>
      [JsonProperty(PropertyName = "transactionsRoot")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactionsRoot")]
#endif
        public string TransactionsRoot { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - the root of the final state trie of the block.
        /// </summary>
      [JsonProperty(PropertyName = "stateRoot")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("stateRoot")]
#endif
        public string StateRoot { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - the root of the receipts trie of the block. 
        /// </summary>
      [JsonProperty(PropertyName = "receiptsRoot")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("receiptsRoot")]
#endif
        public string ReceiptsRoot { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - the address of the beneficiary to whom the mining rewards were given.
        /// </summary>
      [JsonProperty(PropertyName = "miner")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("miner")]
#endif
        public string Miner { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the difficulty for this block.   
        /// </summary>
      [JsonProperty(PropertyName = "difficulty")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("difficulty")]
#endif
        public HexBigInteger Difficulty { get; set; } 

        /// <summary>
        ///     QUANTITY - integer of the total difficulty of the chain until this block. Omitted
        ///     post-merge (geth no longer reports a cumulative PoW difficulty once a chain has
        ///     transitioned) — never fabricate a value here.
        /// </summary>
      [JsonProperty(PropertyName = "totalDifficulty", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("totalDifficulty")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger TotalDifficulty { get; set; }

        /// <summary>
        ///     DATA - the "mix hash" field of this block.  
        /// </summary>
      [JsonProperty(PropertyName = "mixHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("mixHash")]
#endif
        public string MixHash { get; set; }

        /// <summary>
        ///     DATA - the "extra data" field of this block.  
        /// </summary>
      [JsonProperty(PropertyName = "extraData")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("extraData")]
#endif
        public string ExtraData { get; set; }

        /// <summary>
        ///     QUANTITY - integer the size of this block in bytes. 
        /// </summary>
      [JsonProperty(PropertyName = "size")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("size")]
#endif
        public HexBigInteger Size { get; set; }

        /// <summary>
        ///     QUANTITY - the maximum gas allowed in this block. 
        /// </summary>
      [JsonProperty(PropertyName = "gasLimit")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gasLimit")]
#endif
        public HexBigInteger GasLimit { get; set; }

        /// <summary>
        ///     QUANTITY - the total used gas by all transactions in this block. 
        /// </summary>
      [JsonProperty(PropertyName = "gasUsed")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gasUsed")]
#endif
        public HexBigInteger GasUsed { get; set; }

        /// <summary>
        ///     QUANTITY - the unix timestamp for when the block was collated.
        /// </summary>
       [JsonProperty(PropertyName = "timestamp")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("timestamp")]
#endif
        public HexBigInteger Timestamp { get; set; }

        /// <summary>
        ///     Array - Array of uncle hashes.
        /// </summary>
       [JsonProperty(PropertyName = "uncles")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("uncles")]
#endif
        public string[] Uncles { get; set; }

        /// <summary>
        ///     QUANTITY - the base fee per gas. Present from London onward; omitted for pre-London
        ///     blocks (geth does not emit the key when the header has no base fee).
        /// </summary>
        [JsonProperty(PropertyName = "baseFeePerGas", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("baseFeePerGas")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger BaseFeePerGas { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - the root of the withdrawals trie of the block.
        /// </summary>
        [JsonProperty(PropertyName = "withdrawalsRoot", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("withdrawalsRoot")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger WithdrawalsRoot { get; set; }

        /// <summary>
        ///     Array - Array of withdrawals objects. Present (possibly empty) from Shanghai onward;
        ///     omitted for pre-Shanghai blocks.
        /// </summary>
        [JsonProperty(PropertyName = "withdrawals", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("withdrawals")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public Withdrawal[] Withdrawals { get; set; }

        [JsonProperty(PropertyName = "blobGasUsed", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blobGasUsed")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger BlobGasUsed { get; set; }

        [JsonProperty(PropertyName = "excessBlobGas", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("excessBlobGas")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger ExcessBlobGas { get; set; }

        [JsonProperty(PropertyName = "parentBeaconBlockRoot", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("parentBeaconBlockRoot")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string ParentBeaconBlockRoot { get; set; }

        [JsonProperty(PropertyName = "requestsHash", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("requestsHash")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string RequestsHash { get; set; }

        [JsonProperty(PropertyName = "blockAccessListHash", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockAccessListHash")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string BlockAccessListHash { get; set; }

        [JsonProperty(PropertyName = "slotNumber", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("slotNumber")]
[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
#endif
        public HexBigInteger SlotNumber { get; set; }

    }
}

