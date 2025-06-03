using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanBeaconWithdrawalResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("blockNumber")]
#else
        [JsonProperty("blockNumber")]
#endif
        public string BlockNumber { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("amount")]
#else
        [JsonProperty("amount")]
#endif
        public string Amount { get; set; }
    }
}