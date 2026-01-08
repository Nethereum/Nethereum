using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyDeploymentInfo
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("transactionHash")]
#else
        [JsonProperty("transactionHash")]
#endif
        public string TransactionHash { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("blockNumber")]
#else
        [JsonProperty("blockNumber")]
#endif
        public string BlockNumberString { get; set; }

        public long? BlockNumber => string.IsNullOrEmpty(BlockNumberString) ? (long?)null : long.Parse(BlockNumberString);

#if NET8_0_OR_GREATER
        [JsonPropertyName("transactionIndex")]
#else
        [JsonProperty("transactionIndex")]
#endif
        public string TransactionIndexString { get; set; }

        public int? TransactionIndex => string.IsNullOrEmpty(TransactionIndexString) ? (int?)null : int.Parse(TransactionIndexString);

#if NET8_0_OR_GREATER
        [JsonPropertyName("deployer")]
#else
        [JsonProperty("deployer")]
#endif
        public string Deployer { get; set; }
    }
}
