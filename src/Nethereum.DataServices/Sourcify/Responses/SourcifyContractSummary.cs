using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyContractSummary
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("address")]
#else
        [JsonProperty("address")]
#endif
        public string Address { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("match")]
#else
        [JsonProperty("match")]
#endif
        public string Match { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chainId")]
#else
        [JsonProperty("chainId")]
#endif
        public string ChainIdString { get; set; }

        public long ChainId => string.IsNullOrEmpty(ChainIdString) ? 0 : long.Parse(ChainIdString);

#if NET8_0_OR_GREATER
        [JsonPropertyName("matchId")]
#else
        [JsonProperty("matchId")]
#endif
        public string MatchId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("verifiedAt")]
#else
        [JsonProperty("verifiedAt")]
#endif
        public string VerifiedAt { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("contractName")]
#else
        [JsonProperty("contractName")]
#endif
        public string ContractName { get; set; }
    }
}
