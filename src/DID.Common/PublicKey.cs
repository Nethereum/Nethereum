using Newtonsoft.Json;

namespace Did.Common
{
    public class PublicKey
    {

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("ethereumAddress")]
        public string EthereumAddress { get; set; }

        [JsonProperty("publicKeyBase64")]
        public string PublicKeyBase64 { get; set; }

        [JsonProperty("publicKeyBase58")]
        public string PublicKeyBase58 { get; set; }

        [JsonProperty("publicKeyHex")]
        public string PublicKeyHex { get; set; }

        [JsonProperty("publicKeyPem")]
        public string PublicKeyPem { get; set; }
       
    }

}
