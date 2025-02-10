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
        public string Sender { get; set; }

        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

        [JsonProperty(PropertyName = "initCode")]
        public string InitCode { get; set; }

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

        [JsonProperty(PropertyName = "signature")]
        public string Signature { get; set; }

        [JsonProperty(PropertyName = "paymasterAndData")]
        public string PaymasterAndData { get; set; }
        
    }
}
