using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    public class UserOperation
    {
        [JsonProperty(PropertyName = "sender")]
        public string Sender { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

        [JsonProperty(PropertyName = "factory")]
        public string Factory { get; set; }

        [JsonProperty(PropertyName = "factoryData")]
        public string FactoryData { get; set; }

        [JsonProperty(PropertyName = "callData")]
        public string CallData { get; set; }

        [JsonProperty(PropertyName = "callGasLimit")]
        public HexBigInteger CallGasLimit { get; set; }

        [JsonProperty(PropertyName = "verificationGasLimit")]
        public HexBigInteger VerificationGasLimit { get; set; }

        [JsonProperty(PropertyName = "preVerificationGas")]
        public HexBigInteger PreVerificationGas { get; set; }

        [JsonProperty(PropertyName = "maxFeePerGas")]
        public HexBigInteger MaxFeePerGas { get; set; }

        [JsonProperty(PropertyName = "maxPriorityFeePerGas")]
        public HexBigInteger MaxPriorityFeePerGas { get; set; }

        [JsonProperty(PropertyName = "paymasterVerificationGasLimit")]
        public HexBigInteger PaymasterVerificationGasLimit { get; set; }

        [JsonProperty(PropertyName = "paymasterPostOpGasLimit")]
        public HexBigInteger PaymasterPostOpGasLimit { get; set; }

        [JsonProperty(PropertyName = "signature")]
        public string Signature { get; set; }

        [JsonProperty(PropertyName = "paymaster")]
        public string Paymaster { get; set; }

        [JsonProperty(PropertyName = "paymasterData")]
        public string PaymasterData { get; set; }
    }
}
