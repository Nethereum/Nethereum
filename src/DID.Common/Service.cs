using Newtonsoft.Json;

namespace Did.Common
{
    public class Service
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("serviceEndpoint")]
        public string ServiceEndpoint { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

    }

}
