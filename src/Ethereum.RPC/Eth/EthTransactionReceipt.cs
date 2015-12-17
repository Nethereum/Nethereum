using System;
using Newtonsoft.Json;

namespace Ethereum.RPC.Eth
{
    public class EthTransactionReceipt
    {

        /// <summary>
        /// DATA, 32 Bytes - hash of the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "transactionHash")]
        public string TransactionHash { get; set; }

        /// <summary>
        /// QUANTITY - integer of the transactions index position in the block.
        /// </summary>
        [JsonProperty(PropertyName = "transactionIndex")]
        public string TransactionIndexHex { get; set; }

        /// <summary>
        /// DATA, 32 Bytes - hash of the block where this transaction was in.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash")]
        public string BlockHashHex { get; set; }

        /// <summary>
        /// QUANTITY - block number where this transaction was in.
        /// </summary>
        [JsonProperty(PropertyName = "blockNumber")]
        public string BlockNumberHex { get; set; }

        /// <summary>
        /// QUANTITY - The total amount of gas used when this transaction was executed in the block.
        /// </summary>
        [JsonProperty(PropertyName = "cumulativeGasUsed")]
        public string CumulativeGasUsedHex { get; set; }

        /// <summary>
        /// QUANTITY - The amount of gas used by this specific transaction alone.
        /// </summary>
        [JsonProperty(PropertyName = "gasUsed")]
        public string GasUsedHex { get; set; }

        /// <summary>
        /// DATA, 20 Bytes - The contract address created, if the transaction was a contract creation, otherwise null.
        /// </summary>
        [JsonProperty(PropertyName = "contractAddress")]
        public string ContractAddress { get; set; }

        ///<summary>
        /// logs: Array - Array of log objects, which this transaction generated.
        /// </summary>
        [JsonProperty(PropertyName = "logs")]
        public dynamic[] Logs { get; set; }

        /// <summary>
        /// QUANTITY - integer of the transactions index position in the block.
        /// </summary>
       
        //public Int64? TransactionIndex => TransactionIndexHex.ConvertHexToNullableInt64();

        ///// <summary>
        ///// DATA, 32 Bytes - hash of the block where this transaction was in.
        ///// </summary>
       
        //public Int64? BlockHash => BlockHashHex.ConvertHexToNullableInt64();

        ///// <summary>
        ///// QUANTITY - block number where this transaction was in.
        ///// </summary>

        //public Int64? BlockNumber => BlockNumberHex.ConvertHexToNullableInt64();

        ///// <summary>
        ///// QUANTITY - The total amount of gas used when this transaction was executed in the block.
        ///// </summary>

        //public Int64? CumulativeGasUsed => CumulativeGasUsedHex.ConvertHexToNullableInt64();

        ///// <summary>
        ///// QUANTITY - The amount of gas used by this specific transaction alone.
        ///// </summary>
        //public Int64? GasUsed => GasUsedHex.ConvertHexToNullableInt64();
    }
}