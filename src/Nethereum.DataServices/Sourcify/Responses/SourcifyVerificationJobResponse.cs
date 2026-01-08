using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyVerificationJobResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("verificationId")]
#else
        [JsonProperty("verificationId")]
#endif
        public string VerificationId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("status")]
#else
        [JsonProperty("status")]
#endif
        public string Status { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("isJobCompleted")]
#else
        [JsonProperty("isJobCompleted")]
#endif
        public bool IsJobCompleted { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("contract")]
#else
        [JsonProperty("contract")]
#endif
        public SourcifyContractResponse Contract { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("error")]
        public JsonElement? Error { get; set; }
#else
        [JsonProperty("error")]
        public object Error { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("contractId")]
#else
        [JsonProperty("contractId")]
#endif
        public string ContractId { get; set; }
    }
}
