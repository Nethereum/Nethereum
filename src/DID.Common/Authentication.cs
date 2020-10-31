using Newtonsoft.Json;

namespace Did.Common
{
    public class Authentication
    {

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("publicKey")]
        public string PublicKey { get; set; }

    }

}
