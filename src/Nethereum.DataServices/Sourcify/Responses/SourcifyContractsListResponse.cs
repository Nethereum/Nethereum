using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyContractsListResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("results")]
#else
        [JsonProperty("results")]
#endif
        public List<SourcifyContractSummary> Results { get; set; }
    }
}
