using System.Collections.Generic;
using Newtonsoft.Json;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.DID
{
    public class Service
    {
        [JsonProperty("id")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("id")]
#endif
        public string Id { get; set; }

        [JsonProperty("type")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("type")]
#endif
        public string Type { get; set; }

        [JsonProperty("serviceEndpoint")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("serviceEndpoint")]
#endif
        public object ServiceEndpoint { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("description")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string Description { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonExtensionData]
#endif
        public IDictionary<string, object> AdditionalData { get; set; }
    }
}
