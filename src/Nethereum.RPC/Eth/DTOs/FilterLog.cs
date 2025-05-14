using Nethereum.Hex.HexTypes;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class FilterLog
    {
        /// <summary>
        /// true when the log was removed, due to a chain reorganization. false if its a valid log.
        /// </summary>
        [JsonProperty(PropertyName = "removed")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("removed")]
#endif
        public bool Removed { get; set; }
        /// <summary>
        ///     TAG - pending when the log is pending. mined if log is already mined..
        /// </summary>
       [JsonProperty(PropertyName = "type")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("type")]
#endif
        public string Type { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the log index position in the block. null when its pending log.
        /// </summary>
       [JsonProperty(PropertyName = "logIndex")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("logIndex")]
#endif
        public HexBigInteger LogIndex { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the transactions this log was created from. null when its pending log.DATA, 32 Bytes -
        ///     hash of the transaction.
         /// </summary>
        [JsonProperty(PropertyName = "transactionHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactionHash")]
#endif
        public string TransactionHash { get; set; }

        /// <summary>
        ///     QUANTITY - integer of the transactions index position log was created from. null when its pending log.
        /// </summary>
        [JsonProperty(PropertyName = "transactionIndex")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactionIndex")]
#endif
        public HexBigInteger TransactionIndex { get; set; }

        /// <summary>
        ///     DATA, 32 Bytes - hash of the block where this log was in. null when its pending. null when its pending log.
        /// </summary>
        [JsonProperty(PropertyName = "blockHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockHash")]
#endif
        public string BlockHash { get; set; }

        /// <summary>
        ///     QUANTITY - the block number where this log was in. null when its pending. null when its pending log.
        /// </summary>
        [JsonProperty(PropertyName = "blockNumber")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blockNumber")]
#endif
        public HexBigInteger BlockNumber { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - address from which this log originated.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }

        /// <summary>
        ///     DATA - contains one or more 32 Bytes non-indexed arguments of the log.
        /// </summary>
        [JsonProperty(PropertyName = "data")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("data")]
#endif
        public string Data { get; set; }

        /// <summary>
        ///     Array of DATA - Array of 0 to 4 32 Bytes DATA of indexed log arguments.
        ///     (In solidity: The first topic is the hash of the signature of the event (e.g. Deposit(address,bytes32,uint256)),
        ///     except you declared the event with the anonymous specifier.)
        /// </summary>
        [JsonProperty(PropertyName = "topics")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("topics")]
#endif
        public object[] Topics { get; set; }
    }
}