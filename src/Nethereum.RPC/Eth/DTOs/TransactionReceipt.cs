using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Eth.DTOs
{
    public class TransactionReceipt
    {
        /// <summary>
        ///     DATA, 32 Bytes - hash of the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "transactionHash")]
        public string TransactionHash { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the transactions index position in the block.
        /// </summary>
        [JsonProperty(PropertyName = "transactionIndex")]
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block where this transaction was in.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash")]
        public string BlockHash { get; set; }

        /// <summary>
        ///     QUANTITY - block number where this transaction was in.
        /// </summary>
        [JsonProperty(PropertyName = "blockNumber")]
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///     QUANTITY - The total amount of gas used when this transaction was executed in the block.
        /// </summary>
        [JsonProperty(PropertyName = "cumulativeGasUsed")]
        public HexBigInteger CumulativeGasUsed { get; set; }

        /// <summary>
        ///     QUANTITY - The amount of gas used by this specific transaction alone.
        /// </summary>
        [JsonProperty(PropertyName = "gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The contract address created, if the transaction was a contract creation, otherwise null.
        /// </summary>
        [JsonProperty(PropertyName = "contractAddress")]
        public string ContractAddress { get; set; }

        /// <summary>
        ///     logs: Array - Array of log objects, which this transaction generated.
        /// </summary>
        [JsonProperty(PropertyName = "logs")]
        public JArray Logs { get; set; }
    }
}