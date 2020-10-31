using Newtonsoft.Json;
using System.Collections.Generic;

namespace Did.Common
{
    public class DidDocument
    {

        [JsonProperty("@context")]
        public string Context { get; set; } = "https://w3id.org/did/v1";

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("publicKey")]
        public IEnumerable<PublicKey> PublicKey { get; set; }

        [JsonProperty("authentication")]
        public IEnumerable<Authentication> Authentication { get; set; }

        [JsonProperty("uportProfile")]
        public object UportProfile { get; set; }

        [JsonProperty("service")]
        public IEnumerable<Service> Service { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("proof")]
        public LinkedDataProof Proof { get; set; }

    }

}
