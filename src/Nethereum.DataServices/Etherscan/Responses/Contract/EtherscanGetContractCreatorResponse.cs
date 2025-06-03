using System;


#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanGetContractCreatorResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("contractAddress")]
#else
        [JsonProperty("contractAddress")]
#endif
        public string ContractAddress { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("contractCreator")]
#else
        [JsonProperty("contractCreator")]
#endif
        public string ContractCreator { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("txHash")]
#else
        [JsonProperty("txHash")]
#endif
        public string TxHash { get; set; }
    }

    public class EtherscanVerifyContractResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("status")]
#else
        [JsonProperty("status")]
#endif
        public string Status { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("message")]
#else
        [JsonProperty("message")]
#endif
        public string Message { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("result")]
#else
        [JsonProperty("result")]
#endif
        public string Guid { get; set; }
    }

    public class EtherscanCheckVerificationStatusResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("status")]
#else
        [JsonProperty("status")]
#endif
        public string Status { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("message")]
#else
        [JsonProperty("message")]
#endif
        public string Message { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("result")]
#else
        [JsonProperty("result")]
#endif
        public string Result { get; set; } // e.g. "Pass - Verified"
    }
}