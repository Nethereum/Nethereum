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

        [JsonProperty("elfHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("elfHash")]
#endif
        public string ElfHash { get; set; }

        [JsonProperty("proverMode")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("proverMode")]
#endif
        public string ProverMode { get; set; }

        [JsonProperty("stateRootVerified")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("stateRootVerified")]
#endif
        public bool StateRootVerified { get; set; }

        [JsonProperty("proverComputedStateRoot")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("proverComputedStateRoot")]
#endif
        public string ProverComputedStateRoot { get; set; }

        [JsonProperty("gasUsed")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("gasUsed")]
#endif
        public long GasUsed { get; set; }

        [JsonProperty("proverComputedBlockHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("proverComputedBlockHash")]
#endif
        public string ProverComputedBlockHash { get; set; }

        [JsonProperty("blockHashVerified")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockHashVerified")]
#endif
        public bool BlockHashVerified { get; set; }
    }
}
