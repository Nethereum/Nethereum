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
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("hash")]
#endif
        public string TransactionHash { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the transactions index position in the block. null when its pending.
        /// </summary>

        [JsonProperty(PropertyName = "transactionIndex")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactionIndex")]
#endif
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///    QUANTITY - The transaction type.
        /// </summary>

       [JsonProperty(PropertyName = "type")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("type")]
#endif
        public HexBigInteger Type { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block where this transaction was in. null when its pending.
        /// </summary>

       [JsonProperty(PropertyName = "blockHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockHash")]
#endif
        public string BlockHash { get; set; }

        /// <summary>
        ///     QUANTITY - block number where this transaction was in. null when its pending.
        /// </summary>

       [JsonProperty(PropertyName = "blockNumber")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockNumber")]
#endif
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The address the transaction is send from.
        /// </summary>

       [JsonProperty(PropertyName = "from")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("from")]
#endif
        public string From { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - address of the receiver. null when its a contract creation transaction.
        /// </summary>

       [JsonProperty(PropertyName = "to")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("to")]
#endif
        public string To { get; set; }

        /// <summary>
        ///   QUANTITY - gas provided by the sender.
        /// </summary>

       [JsonProperty(PropertyName = "gas")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gas")]
#endif
        public HexBigInteger Gas { get; set; }

        /// <summary>
        ///   QUANTITY - gas price provided by the sender in Wei.
        /// </summary>
 
       [JsonProperty(PropertyName = "gasPrice")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("gasPrice")]
#endif
        public HexBigInteger GasPrice { get; set; }

        /// <summary>
        ///   QUANTITY - Max Fee Per Gas provided by the sender in Wei.
        /// </summary>

      [JsonProperty(PropertyName = "maxFeePerGas")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("maxFeePerGas")]
#endif
        public HexBigInteger MaxFeePerGas { get; set; }

        /// <summary>
        ///   QUANTITY - Max Priority Fee Per Gas provided by the sender in Wei.
        /// </summary>

      [JsonProperty(PropertyName = "maxPriorityFeePerGas")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("maxPriorityFeePerGas")]
#endif
        public HexBigInteger MaxPriorityFeePerGas { get; set; }

        /// <summary>
        ///     QUANTITY - value transferred in Wei.
        /// </summary>

      [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
        public HexBigInteger Value { get; set; }

        /// <summary>
        ///     DATA - the data send along with the transaction.
        /// </summary>

      [JsonProperty(PropertyName = "input")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("input")]
#endif
        public string Input { get; set; }

        /// <summary>
        ///     QUANTITY - the number of transactions made by the sender prior to this one.
        /// </summary>

      [JsonProperty(PropertyName = "nonce")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nonce")]
#endif
        public HexBigInteger Nonce { get; set; }

        /// <summary>
        ///     QUANTITY - r signature.
        /// </summary>

      [JsonProperty(PropertyName = "r")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("r")]
#endif
        public string R { get; set; }


        /// <summary>
        ///     QUANTITY - s signature.
        /// </summary>
 
      [JsonProperty(PropertyName = "s")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("s")]
#endif
        public string S { get; set; }

        /// <summary>
        ///     QUANTITY - v signature.
        /// </summary>
 
      [JsonProperty(PropertyName = "v")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("v")]
#endif
        public string V { get; set; }

        /// <summary>
        ///   Access list
        /// </summary>

      [JsonProperty(PropertyName = "accessList")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("accessList")]
#endif
      public List<AccessList> AccessList { get; set; }

    /// <summary>
    ///   Authorisation list
    /// </summary>

      [JsonProperty(PropertyName = "authorizationList")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("authorizationList")]
#endif
      public List<Authorisation> AuthorisationList { get; set; }
    }
}