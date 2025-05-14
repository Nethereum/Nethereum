using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    public class UserOperationReceipt
    {
        [JsonProperty(PropertyName = "userOpHash")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("userOpHash")]
#endif
        public string UserOpHash { get; set; }

        [JsonProperty(PropertyName = "entryPoint")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("entryPoint")]
#endif
        public string EntryPoint { get; set; }

        [JsonProperty(PropertyName = "sender")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("sender")]
#endif
        public string Sender { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("nonce")]
        #endif
        public HexBigInteger Nonce { get; set; }

        [JsonProperty(PropertyName = "paymaster")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("paymaster")]
        #endif
        public string Paymaster { get; set; }

        [JsonProperty(PropertyName = "actualGasCost")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("actualGasCost")]
        #endif
        public HexBigInteger ActualGasCost { get; set; }

        [JsonProperty(PropertyName = "actualGasUsed")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("actualGasUsed")]
        #endif
        public HexBigInteger ActualGasUsed { get; set; }

        [JsonProperty(PropertyName = "success")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        #endif
        public bool Success { get; set; }

        [JsonProperty(PropertyName = "reason")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("reason")]
        #endif
        public string Reason { get; set; }

        [JsonProperty(PropertyName = "logs")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("logs")]
        #endif
        public List<string> Logs { get; set; }

        [JsonProperty(PropertyName = "receipt")]
        #if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("receipt")]
        #endif
        public TransactionReceipt Receipt { get; set; }
    }
}
