using System.Collections.Generic;
using System.Runtime.Serialization;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class Transaction
    {
        /// <summary>
        ///     DATA, 32 Bytes - hash of the transaction.
        /// </summary>
        [JsonProperty(PropertyName = "hash")]
        [DataMember(Name = "hash")]
        public string TransactionHash { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the transactions index position in the block. null when its pending.
        /// </summary>
        [JsonProperty(PropertyName = "transactionIndex")]
        [DataMember(Name = "transactionIndex")]
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///    QUANTITY - The transaction type.
        /// </summary>
       [JsonProperty(PropertyName = "type")]
       [DataMember(Name = "type")]
        public HexBigInteger Type { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block where this transaction was in. null when its pending.
        /// </summary>
       [JsonProperty(PropertyName = "blockHash")]
       [DataMember(Name = "blockHash")]
        public string BlockHash { get; set; }

        /// <summary>
        ///     QUANTITY - block number where this transaction was in. null when its pending.
        /// </summary>
       [JsonProperty(PropertyName = "blockNumber")]
       [DataMember(Name = "blockNumber")]
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The address the transaction is send from.
        /// </summary>
       [JsonProperty(PropertyName = "from")]
       [DataMember(Name = "from")]
        public string From { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - address of the receiver. null when its a contract creation transaction.
        /// </summary>
       [JsonProperty(PropertyName = "to")]
       [DataMember(Name = "to")]
        public string To { get; set; }

        /// <summary>
        ///   QUANTITY - gas provided by the sender.
        /// </summary>
       [JsonProperty(PropertyName = "gas")]
       [DataMember(Name = "gas")]
        public HexBigInteger Gas { get; set; }

        /// <summary>
        ///   QUANTITY - gas price provided by the sender in Wei.
        /// </summary>
       [JsonProperty(PropertyName = "gasPrice")]
       [DataMember(Name = "gasPrice")]
        public HexBigInteger GasPrice { get; set; }

        /// <summary>
        ///   QUANTITY - Max Fee Per Gas provided by the sender in Wei.
        /// </summary>
      [JsonProperty(PropertyName = "maxFeePerGas")]
      [DataMember(Name = "maxFeePerGas")]
        public HexBigInteger MaxFeePerGas { get; set; }

        /// <summary>
        ///   QUANTITY - Max Priority Fee Per Gas provided by the sender in Wei.
        /// </summary>
      [JsonProperty(PropertyName = "maxPriorityFeePerGas")]
      [DataMember(Name = "maxPriorityFeePerGas")]
        public HexBigInteger MaxPriorityFeePerGas { get; set; }

        /// <summary>
        ///     QUANTITY - value transferred in Wei.
        /// </summary>
      [JsonProperty(PropertyName = "value")]
      [DataMember(Name = "value")]
        public HexBigInteger Value { get; set; }

        /// <summary>
        ///     DATA - the data send along with the transaction.
        /// </summary>
      [JsonProperty(PropertyName = "input")]
      [DataMember(Name = "input")]
        public string Input { get; set; }

        /// <summary>
        ///     QUANTITY - the number of transactions made by the sender prior to this one.
        /// </summary>
      [JsonProperty(PropertyName = "nonce")]
      [DataMember(Name = "nonce")]
        public HexBigInteger Nonce { get; set; }

        /// <summary>
        ///     QUANTITY - r signature.
        /// </summary>
      [JsonProperty(PropertyName = "r")]
      [DataMember(Name = "r")]
        public string R { get; set; }


        /// <summary>
        ///     QUANTITY - s signature.
        /// </summary>
      [JsonProperty(PropertyName = "s")]
      [DataMember(Name = "s")]
        public string S { get; set; }

        /// <summary>
        ///     QUANTITY - v signature.
        /// </summary>
      [JsonProperty(PropertyName = "v")]
      [DataMember(Name = "v")]
        public string V { get; set; }

        /// <summary>
        ///   Access list
        /// </summary>
      [JsonProperty(PropertyName = "accessList")]
      [DataMember(Name = "accessList")]
        public List<AccessList> AccessList { get; set; }
    }
}