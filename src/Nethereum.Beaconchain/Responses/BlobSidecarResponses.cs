using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.Beaconchain.Responses
{
    public class BlobSidecarResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("version")]
#else
        [JsonProperty("version")]
#endif
        public string Version { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("data")]
#else
        [JsonProperty("data")]
#endif
        public List<BlobSidecarData> Data { get; set; }
    }

    public class BlobSidecarData
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("index")]
#else
        [JsonProperty("index")]
#endif
        public string Index { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("blob")]
#else
        [JsonProperty("blob")]
#endif
        public string Blob { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("kzg_commitment")]
#else
        [JsonProperty("kzg_commitment")]
#endif
        public string KzgCommitment { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("kzg_proof")]
#else
        [JsonProperty("kzg_proof")]
#endif
        public string KzgProof { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signed_block_header")]
#else
        [JsonProperty("signed_block_header")]
#endif
        public object SignedBlockHeader { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("kzg_commitment_inclusion_proof")]
#else
        [JsonProperty("kzg_commitment_inclusion_proof")]
#endif
        public List<string> KzgCommitmentInclusionProof { get; set; }
    }
}
