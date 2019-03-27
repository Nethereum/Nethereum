using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System.Collections;

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
        ///  Seal fiels. 
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
    }
}


/*
 * {"author":"0x2a65aca4d5fc5b5c859090a6c34d164135398226","difficulty":"0x5d0214938cba3","extraData":"0x4477617266506f6f6c","gasLimit":"0x66528e","gasUsed":"0x0","hash":"0xd13f8ef8073f3b3a1835f08ca2a0ebda4ea98e8ba6dcf732fa640d5c016bb37e","logsBloom":"0x00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000","miner":"0x2a65aca4d5fc5b5c859090a6c34d164135398226","mixHash":"0x65e828ca58b19ded63c08179fc9f3bdb4a7cdd38bd6e23f2e447af9841fdb276","nonce":"0xe4d67da006fabc21","number":"0x3f0e25","parentHash":"0x700248ba6410b8aa53f99ebb3b08470f577c0af4db98a6e58484d58871d21cff","receiptsRoot":"0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421","sealFields":["0x65e828ca58b19ded63c08179fc9f3bdb4a7cdd38bd6e23f2e447af9841fdb276","0xe4d67da006fabc21"],"sha3Uncles":"0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347","size":"0x20f","stateRoot":"0x2882a3f84343161f4484866d265d3031cc28ef30fbad5612f5acc21e45fafb8d","timestamp":"0x5989c508","totalDifficulty":"0x23187942151f3a3170","transactions":[],"transactionsRoot":"0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421","uncles":[]
 */