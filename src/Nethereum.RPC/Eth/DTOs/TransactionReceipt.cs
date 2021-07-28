using Nethereum.Hex.HexTypes;
using Nethereum.Util;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace Nethereum.RPC.Eth.DTOs
{
    [DataContract]
    public class TransactionReceipt
    {
        /// <summary>
        ///     DATA, 32 Bytes - hash of the transaction.
        /// </summary>
        [DataMember(Name = "transactionHash")]
        public string TransactionHash { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the transactions index position in the block.
        /// </summary>
       [DataMember(Name = "transactionIndex")]
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block where this transaction was in.
        /// </summary>
       [DataMember(Name = "blockHash")]
        public string BlockHash { get; set; }

        /// <summary>
        ///     QUANTITY - block number where this transaction was in.
        /// </summary>
       [DataMember(Name = "blockNumber")]
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///     QUANTITY - The total amount of gas used when this transaction was executed in the block.
        /// </summary>
       [DataMember(Name = "cumulativeGasUsed")]
        public HexBigInteger CumulativeGasUsed { get; set; }

        /// <summary>
        ///     QUANTITY - The amount of gas used by this specific transaction alone.
        /// </summary>
       [DataMember(Name = "gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        /// <summary>
        /// The actual value per gas deducted from the senders account. Before EIP-1559, this is equal to the transaction's gas price. After, it is equal to baseFeePerGas + min(maxFeePerGas - baseFeePerGas, maxPriorityFeePerGas). Legacy transactions and EIP-2930 transactions are coerced into the EIP-1559 format by setting both maxFeePerGas and maxPriorityFeePerGas as the transaction's gas price.
        /// </summary>
        [DataMember(Name = "effectiveGasPrice")]
        public HexBigInteger EffectiveGasPrice { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The contract address created, if the transaction was a contract creation, otherwise null.
        /// </summary>
       [DataMember(Name = "contractAddress")]
        public string ContractAddress { get; set; }

        /// <summary>
        ///     QUANTITY / BOOLEAN Transaction Success 1, Transaction Failed 0
        /// </summary>
       [DataMember(Name = "status")]
        public HexBigInteger Status { get; set; }

        /// <summary>
        ///     logs: Array - Array of log objects, which this transaction generated.
        /// </summary>
       [DataMember(Name = "logs")]
        public JArray Logs { get; set; }

        /// <summary>
        ///    QUANTITY - The transaction type.
        /// </summary>
       [DataMember(Name = "type")]
        public HexBigInteger Type { get; set; }

        /// <summary>
        ///     DATA, 256 Bytes - Bloom filter for light clients to quickly retrieve related logs
        /// </summary>
       [DataMember(Name = "logsBloom")]
        public string LogsBloom { get; set; }

        public bool? HasErrors()
        {
            if (Status?.HexValue == null) return null;
            return Status.Value == 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is TransactionReceipt val)
            {
                return TransactionHash == val.TransactionHash &&
                       TransactionIndex == val.TransactionIndex &&
                       BlockHash == val.BlockHash &&
                       BlockNumber == val.BlockNumber &&
                       CumulativeGasUsed == val.CumulativeGasUsed &&
                       GasUsed == val.GasUsed &&
                       ContractAddress.IsTheSameAddress(val.ContractAddress) &&
                       Status == val.Status &&
                       Type == val.Type &&
                       LogsBloom == val.LogsBloom;
            }

            return false;
        }
    }
}
