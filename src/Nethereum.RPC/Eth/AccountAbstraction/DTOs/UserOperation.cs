using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    public class UserOperation
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

        [JsonProperty(PropertyName = "factory")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("factory")]
#endif
        public string Factory { get; set; }

        [JsonProperty(PropertyName = "factoryData")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("factoryData")]
#endif
        public string FactoryData { get; set; }

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

        [JsonProperty(PropertyName = "paymasterVerificationGasLimit")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("paymasterVerificationGasLimit")]
#endif
        public HexBigInteger PaymasterVerificationGasLimit { get; set; }

        [JsonProperty(PropertyName = "paymasterPostOpGasLimit")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("paymasterPostOpGasLimit")]
#endif
        public HexBigInteger PaymasterPostOpGasLimit { get; set; }

        [JsonProperty(PropertyName = "signature")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("signature")]
#endif
        public string Signature { get; set; }

        [JsonProperty(PropertyName = "paymaster")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("paymaster")]
#endif
        public string Paymaster { get; set; }

        [JsonProperty(PropertyName = "paymasterData")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("paymasterData")]
#endif
        public string PaymasterData { get; set; }

        // EIP-7702 authorisation tuple ridden alongside the userOp on the RPC wire (v0.9 EntryPoint).
        // A side-channel: it is not part of the packed userOp form, so it lives only on this DTO.
        // Optional and omitted when unset (NullValueHandling.Ignore) to stay wire-identical for ops without it.
        [JsonProperty(PropertyName = "eip7702Auth")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("eip7702Auth")]
#endif
        public Authorisation Eip7702Auth { get; set; }
    }
}
