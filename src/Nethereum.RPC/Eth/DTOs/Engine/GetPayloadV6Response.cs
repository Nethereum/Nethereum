using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class GetPayloadV6Response
    {
        [JsonProperty(PropertyName = "executionPayload")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("executionPayload")]
#endif
        public ExecutionPayloadV4 ExecutionPayload { get; set; }

        [JsonProperty(PropertyName = "blockValue")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockValue")]
#endif
        public HexBigInteger BlockValue { get; set; }

        [JsonProperty(PropertyName = "blobsBundle")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blobsBundle")]
#endif
        public BlobsBundleV1 BlobsBundle { get; set; } = new BlobsBundleV1();

        [JsonProperty(PropertyName = "shouldOverrideBuilder")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("shouldOverrideBuilder")]
#endif
        public bool ShouldOverrideBuilder { get; set; }

        [JsonProperty(PropertyName = "executionRequests")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("executionRequests")]
#endif
        public List<string> ExecutionRequests { get; set; } = new List<string>();
    }
}
