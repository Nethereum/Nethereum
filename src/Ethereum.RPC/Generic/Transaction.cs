using Newtonsoft.Json;

namespace Ethereum.RPC.Generic
{
    /// <summary>
    /// Block including transaction objects
    /// </summary>
    public class BlockWithTransactions : Block
    {
        /// <summary>
        /// Array - Array of transaction objects
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public Transaction[] Transactions { get; set; }

    }

    /// <summary>
    /// Block including just the transaction hashes
    /// </summary>
    public class BlockWithTransactionHashes : Block
    {
        /// <summary>
        /// Array - Array of transaction hashes
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
        public string[] TransactionHashes { get; set; }

    }

    public class Block
    {
        
        /// <summary>
        /// QUANTITY - the block number. null when its pending block.
        /// </summary>
        [JsonProperty(PropertyName = "number")]
        public HexBigInteger Number { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - hash of the block.
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        public string BlockHash { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - hash of the parent block.
        /// </summary>
        [JsonProperty(PropertyName = "parentHash")]
        public string ParentHash { get; set; }

        /// <summary>
        /// DATA, 8 Bytes - hash of the generated proof-of-work. null when its pending block.
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public string Nonce { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - SHA3 of the uncles data in the block.
        /// </summary>
        [JsonProperty(PropertyName = "sha3Uncles")]
        public string Sha3Uncles { get; set; }

        
        /// <summary>
        /// DATA, 256 Bytes - the bloom filter for the logs of the block. null when its pending block.
        /// </summary>
        [JsonProperty(PropertyName = "logsBloom")]
        public string LogsBloom { get; set; }


        /// <summary>
        /// DATA, 32 Bytes - the root of the transaction trie of the block.
        /// </summary>
        [JsonProperty(PropertyName = "transactionsRoot")]
        public string TransactionsRoot { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - the root of the final state trie of the block.
        /// </summary>
        [JsonProperty(PropertyName = "stateRoot")]
        public string StateRoot { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - the root of the receipts trie of the block.
        /// </summary>
        [JsonProperty(PropertyName = "receiptsRoot")]
        public string ReceiptsRoot { get; set; }

 
        /// <summary>
        /// DATA, 20 Bytes - the address of the beneficiary to whom the mining rewards were given.
        /// </summary>
        [JsonProperty(PropertyName = "miner")]
        public string Miner { get; set; }

        /// <summary>
        /// QUANTITY - integer of the difficulty for this block.
        /// </summary>
        [JsonProperty(PropertyName = "difficulty")]
        public HexBigInteger Difficulty { get; set; }

        /// <summary>
        /// QUANTITY - integer of the total difficulty of the chain until this block.
        /// </summary>
        [JsonProperty(PropertyName = "totalDifficulty")]
        public HexBigInteger TotalDifficulty { get; set; }

        /// <summary>
        ///  DATA - the "extra data" field of this block.
        /// </summary>
        [JsonProperty(PropertyName = "extraData")]
        public string ExtraData { get; set; }

        /// <summary>
        ///QUANTITY - integer the size of this block in bytes.
        /// </summary>
        [JsonProperty(PropertyName = "size")]
        public HexBigInteger Size { get; set; }

        /// <summary>
        ///QUANTITY - the maximum gas allowed in this block.
        /// </summary>
        [JsonProperty(PropertyName = "gasLimit")]
        public HexBigInteger GasLimit { get; set; }

        /// <summary>
        ///QUANTITY - the total used gas by all transactions in this block.
        /// </summary>
        [JsonProperty(PropertyName = "gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        /// <summary>
        ///QUANTITY - the unix timestamp for when the block was collated.
        /// </summary>
        [JsonProperty(PropertyName = "timestamp")]
        public HexBigInteger Timestamp { get; set; }

        /// <summary>
        /// Array - Array of uncle hashes.
        /// </summary>
        [JsonProperty(PropertyName = "uncles")]
        public string[] Uncles { get; set; }
    }

    public class Transaction
    {
        /// <summary>
        /// DATA, 32 Bytes - hash of the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        public string TransactionHash { get; set; }

        /// <summary>
        /// QUANTITY - integer of the transactions index position in the block. null when its pending.
        /// </summary>
        [JsonProperty(PropertyName = "transactionIndex")]
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///DATA, 32 Bytes - hash of the block where this transaction was in. null when its pending.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash")]
        public string BlockHash { get; set; }

        /// <summary>
        /// QUANTITY - block number where this transaction was in. null when its pending.
        /// </summary>
        [JsonProperty(PropertyName = "blockNumber")]
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///  DATA, 20 Bytes - The address the transaction is send from.
        /// </summary>
        [JsonProperty(PropertyName = "from")]
        public string From { get; set; }

        /// <summary>
        ///DATA, 20 Bytes - address of the receiver. null when its a contract creation transaction.
        /// </summary>
        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }

        /// <summary>
        /// QUANTITY - gas provided by the sender.
        /// </summary>
        [JsonProperty(PropertyName = "gas")]
        public HexBigInteger Gas { get; set; }

        /// <summary>
        /// QUANTITY - gas price provided by the sender in Wei.
        /// </summary>
        [JsonProperty(PropertyName = "gasPrice")]
        public HexBigInteger GasPrice { get; set; }

        /// <summary>
        /// QUANTITY - value transferred in Wei.
        /// </summary>
        [JsonProperty(PropertyName = "value")]
        public HexBigInteger Value { get; set; }
        /// <summary>
        ///DATA - the data send along with the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "input")]
        public string Input { get; set; }

        /// <summary>
        /// QUANTITY - the number of transactions made by the sender prior to this one.
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

    }
}