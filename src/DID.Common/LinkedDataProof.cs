using Newtonsoft.Json;

namespace Did.Common
{
    public class LinkedDataProof
    {

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("creator")]
        public string Creator { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("signatureValue")]
        public string SignatureValue { get; set; }

    }

}
