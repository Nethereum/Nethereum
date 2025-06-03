#if NET8_0_OR_GREATER
using Nethereum;
using Nethereum.DataServices;
using Nethereum.DataServices.Etherscan;
using Nethereum.DataServices.Etherscan.Responses;
using Nethereum.DataServices.Etherscan.Responses.Account;
using System.Text.Json.Serialization;
#else
using Nethereum;
using Nethereum.DataServices;
using Nethereum.DataServices.Etherscan;
using Nethereum.DataServices.Etherscan.Responses;
using Nethereum.DataServices.Etherscan.Responses.Account;
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanFundedByResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("address")]
#else
        [JsonProperty("address")]
#endif
        public string Address { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("fundedBy")]
#else
        [JsonProperty("fundedBy")]
#endif
        public string FundedBy { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("age")]
#else
        [JsonProperty("age")]
#endif
        public string Age { get; set; }
    }
}