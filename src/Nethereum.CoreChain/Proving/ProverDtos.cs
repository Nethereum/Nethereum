using Newtonsoft.Json;

namespace Nethereum.CoreChain.Proving
{
    public class ProveBlockRequest
    {
        [JsonProperty("witnessBytes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("witnessBytes")]
#endif
        public string WitnessBytes { get; set; }

        [JsonProperty("preStateRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("preStateRoot")]
#endif
        public string PreStateRoot { get; set; }

        [JsonProperty("postStateRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("postStateRoot")]
#endif
        public string PostStateRoot { get; set; }

        [JsonProperty("blockNumber")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockNumber")]
#endif
        public long BlockNumber { get; set; }
    }

    public class ProveBlockResponse
    {
        [JsonProperty("proofBytes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("proofBytes")]
#endif
        public string ProofBytes { get; set; }

        [JsonProperty("preStateRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("preStateRoot")]
#endif
        public string PreStateRoot { get; set; }

        [JsonProperty("postStateRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("postStateRoot")]
#endif
        public string PostStateRoot { get; set; }

        [JsonProperty("witnessHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("witnessHash")]
#endif
        public string WitnessHash { get; set; }

        [JsonProperty("blockNumber")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockNumber")]
#endif
        public long BlockNumber { get; set; }

        [JsonProperty("proverMode")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("proverMode")]
#endif
        public string ProverMode { get; set; }
    }
}
