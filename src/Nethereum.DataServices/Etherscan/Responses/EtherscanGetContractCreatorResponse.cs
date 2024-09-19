using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses
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
}