using Newtonsoft.Json;
using System.Collections.Generic;

namespace Did.Common
{
    public class DidUrl
    {

        public DidUrl()
        {
            Params = new Dictionary<string, string>();
        }

        [JsonProperty("did")]
        public string Did { get; set; }

        [JsonProperty("didUrl")]
        public string Url { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("fragment")]
        public string Fragment { get; set; }

        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("params")]
        public Dictionary<string, string> Params { get; set; }
    }

}
