using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    public class UserOperationGasEstimate
    {
        [JsonProperty("callGasLimit")]
        public HexBigInteger CallGasLimit { get; set; }

        [JsonProperty("verificationGasLimit")]
        public HexBigInteger VerificationGasLimit { get; set; }

        [JsonProperty("preVerificationGas")]
        public HexBigInteger PreVerificationGas { get; set; }

        [JsonProperty("maxFeePerGas")]
        public HexBigInteger MaxFeePerGas { get; set; }

        [JsonProperty("maxPriorityFeePerGas")]
        public HexBigInteger MaxPriorityFeePerGas { get; set; }
    }
}
