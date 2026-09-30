using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class ChainConfiguration
    {
        [JsonProperty(PropertyName = "current")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("current")]
#endif
        public ChainConfigurationEntry Current { get; set; }

        [JsonProperty(PropertyName = "next")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("next")]
#endif
        public ChainConfigurationEntry Next { get; set; }

        [JsonProperty(PropertyName = "last")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("last")]
#endif
        public ChainConfigurationEntry Last { get; set; }
    }

    public class ChainConfigurationEntry
    {
        [JsonProperty(PropertyName = "activationTime")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("activationTime")]
#endif
        public ulong ActivationTime { get; set; }

        [JsonProperty(PropertyName = "chainId")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("chainId")]
#endif
        public string ChainId { get; set; }

        [JsonProperty(PropertyName = "forkId")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("forkId")]
#endif
        public string ForkId { get; set; }

        [JsonProperty(PropertyName = "blobSchedule")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("blobSchedule")]
#endif
        public BlobScheduleConfiguration BlobSchedule { get; set; }

        [JsonProperty(PropertyName = "precompiles")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("precompiles")]
#endif
        public Dictionary<string, string> Precompiles { get; set; } = new Dictionary<string, string>();

        [JsonProperty(PropertyName = "systemContracts")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("systemContracts")]
#endif
        public Dictionary<string, string> SystemContracts { get; set; } = new Dictionary<string, string>();
    }

    public class BlobScheduleConfiguration
    {
        [JsonProperty(PropertyName = "target")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("target")]
#endif
        public ulong Target { get; set; }

        [JsonProperty(PropertyName = "max")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("max")]
#endif
        public ulong Max { get; set; }

        [JsonProperty(PropertyName = "baseFeeUpdateFraction")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("baseFeeUpdateFraction")]
#endif
        public ulong BaseFeeUpdateFraction { get; set; }
    }
}
