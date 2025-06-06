using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.Util;
using Nethereum.Util.Json;
using Newtonsoft.Json;

namespace Nethereum.GnosisSafe.ContractDefinition
{
    public partial class EncodeTransactionDataFunction : EncodeTransactionDataFunctionBase { }

    
    [Function("encodeTransactionData", "bytes")]
    [Struct("SafeTx")]
    public class EncodeTransactionDataFunctionBase : FunctionMessage
    {
        [JsonProperty(PropertyName = "to")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("to")]
#endif
        [Parameter("address", "to", 1)]
        public string To { get; set; }

        [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
      [System.Text.Json.Serialization.JsonPropertyName("value")]
      [System.Text.Json.Serialization.JsonConverter(typeof(BigIntegerJsonConverter))]
#endif
        [Parameter("uint256", "value", 2)]
        public BigInteger Value { get; set; }

        [JsonProperty(PropertyName = "data")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("data")]
    [System.Text.Json.Serialization.JsonConverter(typeof(HexToByteArrayConverter))]
#endif
        [JsonConverter(typeof(NewtonsoftHexToByteArrayConverter))]
        [Parameter("bytes", "data", 3)]
        public byte[] Data { get; set; }

        [JsonProperty(PropertyName = "operation")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("operation")]
#endif
        [Parameter("uint8", "operation", 4)]
        public byte Operation { get; set; }

        [JsonProperty(PropertyName = "safeTxGas")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("safeTxGas")]
    [System.Text.Json.Serialization.JsonConverter(typeof(BigIntegerJsonConverter))]
#endif
        [Parameter("uint256", "safeTxGas", 5)]
        public BigInteger SafeTxGas { get; set; }

        [JsonProperty(PropertyName = "baseGas")]
#if NET6_0_OR_GREATER
       [System.Text.Json.Serialization.JsonPropertyName("baseGas")]
        [System.Text.Json.Serialization.JsonConverter(typeof(BigIntegerJsonConverter))]
#endif
        [Parameter("uint256", "baseGas", 6)]
        public BigInteger BaseGas { get; set; }

        [JsonProperty(PropertyName = "gasPrice")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("gasPrice")]
        [System.Text.Json.Serialization.JsonConverter(typeof(BigIntegerJsonConverter))]
#endif
        [Parameter("uint256", "gasPrice", 7)]
        public BigInteger SafeGasPrice { get; set; }

        [JsonProperty(PropertyName = "gasToken")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("gasToken")]
#endif
        [Parameter("address", "gasToken", 8)]
        public string GasToken { get; set; }

        [JsonProperty(PropertyName = "refundReceiver")]
#if NET6_0_OR_GREATER
    [System.Text.Json.Serialization.JsonPropertyName("refundReceiver")]
#endif
        [Parameter("address", "refundReceiver", 9)]
        public string RefundReceiver { get; set; }

        [JsonProperty(PropertyName = "nonce")]
#if NET6_0_OR_GREATER
       [System.Text.Json.Serialization.JsonPropertyName("nonce")]
       [System.Text.Json.Serialization.JsonConverter(typeof(BigIntegerJsonConverter))]
#endif
        [Parameter("uint256", "nonce", 10)]
        public BigInteger SafeNonce { get; set; }

#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore]
#endif
        [Newtonsoft.Json.JsonIgnore]
        public override BigInteger? GasPrice
        {
            get => base.GasPrice;
            set => base.GasPrice = value;
        }


#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonIgnore]
#endif
        [Newtonsoft.Json.JsonIgnore]
        public override BigInteger? Nonce
        {
            get => base.Nonce;
            set => base.Nonce = value;
        }
    }

}
