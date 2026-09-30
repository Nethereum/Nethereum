using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class BlobsBundleV1
    {
        [JsonProperty(PropertyName = "commitments")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("commitments")]
#endif
        public List<string> Commitments { get; set; } = new List<string>();

        [JsonProperty(PropertyName = "proofs")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("proofs")]
#endif
        public List<string> Proofs { get; set; } = new List<string>();

        [JsonProperty(PropertyName = "blobs")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blobs")]
#endif
        public List<string> Blobs { get; set; } = new List<string>();
    }
}
