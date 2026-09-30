using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    public class UserOperationGasEstimate
    {
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
    }
}
