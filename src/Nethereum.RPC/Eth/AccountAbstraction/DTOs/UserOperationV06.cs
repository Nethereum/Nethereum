using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{

    public class UserOperationV06
    {
            
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

        [JsonProperty(PropertyName = "initCode")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("initCode")]
#endif
        public string InitCode { get; set; }

        [JsonProperty(PropertyName = "callData")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("callData")]
#endif
        public string CallData { get; set; }

        [JsonProperty(PropertyName = "callGasLimit")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("callGasLimit")]
#endif
        public HexBigInteger CallGasLimit { get; set; }

        [JsonProperty(PropertyName = "verificationGasLimit")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("verificationGasLimit")]
#endif
        public HexBigInteger VerificationGasLimit { get; set; }

        [JsonProperty(PropertyName = "preVerificationGas")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("preVerificationGas")]
#endif
        public HexBigInteger PreVerificationGas { get; set; }

        [JsonProperty(PropertyName = "maxFeePerGas")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("maxFeePerGas")]
#endif
        public HexBigInteger MaxFeePerGas { get; set; }

        [JsonProperty(PropertyName = "maxPriorityFeePerGas")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("maxPriorityFeePerGas")]
#endif
        public HexBigInteger MaxPriorityFeePerGas { get; set; }

        [JsonProperty(PropertyName = "signature")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("signature")]
#endif
        public string Signature { get; set; }

        [JsonProperty(PropertyName = "paymasterAndData")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("paymasterAndData")]
#endif
        public string PaymasterAndData { get; set; }
        
    }
}
