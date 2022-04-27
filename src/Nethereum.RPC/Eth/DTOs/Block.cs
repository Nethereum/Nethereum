using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System.Collections;
using System.Runtime.Serialization;
using Newtonsoft.Json;
namespace Nethereum.RPC.Eth.DTOs
{

    public class Block
    {
        /// <summary>
        ///     QUANTITY - the block number. null when its pending block. 
        /// </summary>
        [JsonProperty(PropertyName = "number")]
        public HexBigInteger Number { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block.  
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        public string BlockHash { get; set; }

        /// <summary>
        ///  block author.
        /// </summary>
        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }


        /// <summary>
        ///  Seal fields. 
        /// </summary>
      [JsonProperty(PropertyName = "sealFields")]
        public string[] SealFields { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the parent block. 
        /// </summary>
      [JsonProperty(PropertyName = "parentHash")]
        public string ParentHash { get; set; }

        /// <summary>
        ///     DATA, 8 Bytes - hash of the generated proof-of-work. null when its pending block. 
        /// </summary>
      [JsonProperty(PropertyName = "nonce")]
        public string Nonce { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - SHA3 of the uncles data in the block. 
        /// </summary>
      [JsonProperty(PropertyName = "sha3Uncles")]
        public string Sha3Uncles { get; set; }


        /// <summary>
        ///     DATA, 256 Bytes - the bloom filter for the logs of the block. null when its pending block. 
        /// </summary>
      [JsonProperty(PropertyName = "logsBloom")]
        public string LogsBloom { get; set; }


        /// <summary>
        ///     DATA, 32 Bytes - the root of the transaction trie of the block.
        /// </summary>
      [JsonProperty(PropertyName = "transactionsRoot")]
        public string TransactionsRoot { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - the root of the final state trie of the block.
        /// </summary>
      [JsonProperty(PropertyName = "stateRoot")]
        public string StateRoot { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - the root of the receipts trie of the block. 
        /// </summary>
      [JsonProperty(PropertyName = "receiptsRoot")]
        public string ReceiptsRoot { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - the address of the beneficiary to whom the mining rewards were given.
        /// </summary>
      [JsonProperty(PropertyName = "miner")]
        public string Miner { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the difficulty for this block.   
        /// </summary>
      [JsonProperty(PropertyName = "difficulty")]
        public HexBigInteger Difficulty { get; set; } 

        /// <summary>
        ///     QUANTITY - integer of the total difficulty of the chain until this block.
        /// </summary>
      [JsonProperty(PropertyName = "totalDifficulty")]
        public HexBigInteger TotalDifficulty { get; set; }

        /// <summary>
        ///     DATA - the "mix hash" field of this block.  
        /// </summary>
      [JsonProperty(PropertyName = "mixHash")]
        public string MixHash { get; set; }

        /// <summary>
        ///     DATA - the "extra data" field of this block.  
        /// </summary>
      [JsonProperty(PropertyName = "extraData")]
        public string ExtraData { get; set; }

        /// <summary>
        ///     QUANTITY - integer the size of this block in bytes. 
        /// </summary>
      [JsonProperty(PropertyName = "size")]
        public HexBigInteger Size { get; set; }

        /// <summary>
        ///     QUANTITY - the maximum gas allowed in this block. 
        /// </summary>
      [JsonProperty(PropertyName = "gasLimit")]
        public HexBigInteger GasLimit { get; set; }

        /// <summary>
        ///     QUANTITY - the total used gas by all transactions in this block. 
        /// </summary>
      [JsonProperty(PropertyName = "gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        /// <summary>
        ///     QUANTITY - the unix timestamp for when the block was collated.
        /// </summary>
       [JsonProperty(PropertyName = "timestamp")]
        public HexBigInteger Timestamp { get; set; }

        /// <summary>
        ///     Array - Array of uncle hashes.
        /// </summary>
       [JsonProperty(PropertyName = "uncles")]
        public string[] Uncles { get; set; }

        /// <summary>
        ///     QUANTITY - the base fee per gas.
        /// </summary>
        [JsonProperty(PropertyName = "baseFeePerGas")]
        public HexBigInteger BaseFeePerGas { get; set; }


    }
}

