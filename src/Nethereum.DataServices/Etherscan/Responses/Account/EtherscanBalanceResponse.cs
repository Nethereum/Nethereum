using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanBalanceResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("account")]
#else
        [JsonProperty("account")]
#endif
        public string Account { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("balance")]
#else
        [JsonProperty("balance")]
#endif
        public string Balance { get; set; }
    }
}