using System;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.ZkProofs.Groth16
{
    public class Groth16ProofJson
    {
#if NET6_0_OR_GREATER
        [JsonPropertyName("pi_a")]
#else
        [JsonProperty("pi_a")]
#endif
        public string[] PiA { get; set; } = new string[0];

#if NET6_0_OR_GREATER
        [JsonPropertyName("pi_b")]
#else
        [JsonProperty("pi_b")]
#endif
        public string[][] PiB { get; set; } = new string[0][];

#if NET6_0_OR_GREATER
        [JsonPropertyName("pi_c")]
#else
        [JsonProperty("pi_c")]
#endif
        public string[] PiC { get; set; } = new string[0];
    }
}
