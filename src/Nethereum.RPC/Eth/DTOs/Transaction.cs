using System.Collections.Generic;
using System.Runtime.Serialization;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Eth.DTOs
{
    [DataContract]
    public class Transaction
    {
        /// <summary>
        ///     DATA, 32 Bytes - hash of the transaction.
        /// </summary>
       [DataMember(Name = "hash")]
        public string TransactionHash { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the transactions index position in the block. null when its pending.
        /// </summary>
       [DataMember(Name = "transactionIndex")]
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///    QUANTITY - The transaction type.
        /// </summary>
       [DataMember(Name = "type")]
        public HexBigInteger Type { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block where this transaction was in. null when its pending.
       [DataMember(Name = "blockHash")]
        public string BlockHash { get; set; }

        /// <summary>
        ///     QUANTITY - block number where this transaction was in. null when its pending.
        /// </summary>
       [DataMember(Name = "blockNumber")]
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The address the transaction is send from.
        /// </summary>
       [DataMember(Name = "from")]
        public string From { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - address of the receiver. null when its a contract creation transaction.
        /// </summary>
       [DataMember(Name = "to")]
        public string To { get; set; }

        /// <summary>
        ///   QUANTITY - gas provided by the sender.
        /// </summary>
       [DataMember(Name = "gas")]
        public HexBigInteger Gas { get; set; }

        /// <summary>
        ///   QUANTITY - gas price provided by the sender in Wei.
        /// </summary>
       [DataMember(Name = "gasPrice")]
        public HexBigInteger GasPrice { get; set; }

        /// <summary>
        ///   QUANTITY - Max Fee Per Gas provided by the sender in Wei.
        /// </summary>
      [DataMember(Name = "maxFeePerGas")]
        public HexBigInteger MaxFeePerGas { get; set; }

        /// <summary>
        ///   QUANTITY - Max Priority Fee Per Gas provided by the sender in Wei.
        /// </summary>
      [DataMember(Name = "maxPriorityFeePerGas")]
        public HexBigInteger MaxPriorityFeePerGas { get; set; }

        /// <summary>
        ///     QUANTITY - value transferred in Wei.
        /// </summary>
      [DataMember(Name = "value")]
        public HexBigInteger Value { get; set; }

        /// <summary>
        ///     DATA - the data send along with the transaction.
        /// </summary>
      [DataMember(Name = "input")]
        public string Input { get; set; }

        /// <summary>
        ///     QUANTITY - the number of transactions made by the sender prior to this one.
        /// </summary>
      [DataMember(Name = "nonce")]
        public HexBigInteger Nonce { get; set; }

        /// <summary>
        ///     QUANTITY - r signature.
        /// </summary>
      [DataMember(Name = "r")]
        public string R { get; set; }


        /// <summary>
        ///     QUANTITY - s signature.
        /// </summary>
      [DataMember(Name = "s")]
        public string S { get; set; }

        /// <summary>
        ///     QUANTITY - v signature.
        /// </summary>
      [DataMember(Name = "v")]
        public string V { get; set; }

        /// <summary>
        ///   Access list
        /// </summary>
      [DataMember(Name = "accessList")]
        public List<AccessList> AccessList { get; set; }
    }
}