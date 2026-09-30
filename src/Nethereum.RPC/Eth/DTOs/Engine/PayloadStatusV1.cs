using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public static class EnginePayloadStatus
    {
        public const string Valid = "VALID";
        public const string Invalid = "INVALID";
        public const string Syncing = "SYNCING";
        public const string Accepted = "ACCEPTED";
        public const string InvalidBlockHash = "INVALID_BLOCK_HASH";
    }

    public class PayloadStatusV1
    {
        [JsonProperty(PropertyName = "status")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("status")]
#endif
        public string Status { get; set; }

        [JsonProperty(PropertyName = "latestValidHash", NullValueHandling = NullValueHandling.Include)]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("latestValidHash")]
#endif
        public string LatestValidHash { get; set; }

        [JsonProperty(PropertyName = "validationError", NullValueHandling = NullValueHandling.Include)]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("validationError")]
#endif
        public string ValidationError { get; set; }
    }
}
