using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyContractResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("match")]
#else
        [JsonProperty("match")]
#endif
        public string Match { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("creationMatch")]
#else
        [JsonProperty("creationMatch")]
#endif
        public string CreationMatch { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("runtimeMatch")]
#else
        [JsonProperty("runtimeMatch")]
#endif
        public string RuntimeMatch { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chainId")]
#else
        [JsonProperty("chainId")]
#endif
        public string ChainIdString { get; set; }

        public long ChainId => string.IsNullOrEmpty(ChainIdString) ? 0 : long.Parse(ChainIdString);

#if NET8_0_OR_GREATER
        [JsonPropertyName("address")]
#else
        [JsonProperty("address")]
#endif
        public string Address { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("verifiedAt")]
#else
        [JsonProperty("verifiedAt")]
#endif
        public string VerifiedAt { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("matchId")]
#else
        [JsonProperty("matchId")]
#endif
        public string MatchId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sources")]
#else
        [JsonProperty("sources")]
#endif
        public Dictionary<string, SourcifySourceFile> Sources { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("abi")]
        public JsonElement? Abi { get; set; }
#else
        [JsonProperty("abi")]
        public object Abi { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("compilation")]
#else
        [JsonProperty("compilation")]
#endif
        public SourcifyCompilationInfo Compilation { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("metadata")]
        public JsonElement? Metadata { get; set; }
#else
        [JsonProperty("metadata")]
        public object Metadata { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("storageLayout")]
        public JsonElement? StorageLayout { get; set; }
#else
        [JsonProperty("storageLayout")]
        public object StorageLayout { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("deployment")]
#else
        [JsonProperty("deployment")]
#endif
        public SourcifyDeploymentInfo Deployment { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signatures")]
#else
        [JsonProperty("signatures")]
#endif
        public SourcifySignaturesInfo Signatures { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("proxyResolution")]
#else
        [JsonProperty("proxyResolution")]
#endif
        public SourcifyProxyResolution ProxyResolution { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("creationBytecode")]
#else
        [JsonProperty("creationBytecode")]
#endif
        public SourcifyBytecodeInfo CreationBytecode { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("runtimeBytecode")]
#else
        [JsonProperty("runtimeBytecode")]
#endif
        public SourcifyBytecodeInfo RuntimeBytecode { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("userdoc")]
        public JsonElement? Userdoc { get; set; }
#else
        [JsonProperty("userdoc")]
        public object Userdoc { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("devdoc")]
        public JsonElement? Devdoc { get; set; }
#else
        [JsonProperty("devdoc")]
        public object Devdoc { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("sourceIds")]
        public JsonElement? SourceIds { get; set; }
#else
        [JsonProperty("sourceIds")]
        public object SourceIds { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("stdJsonInput")]
        public JsonElement? StdJsonInput { get; set; }
#else
        [JsonProperty("stdJsonInput")]
        public object StdJsonInput { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("stdJsonOutput")]
        public JsonElement? StdJsonOutput { get; set; }
#else
        [JsonProperty("stdJsonOutput")]
        public object StdJsonOutput { get; set; }
#endif

        public string GetAbiString()
        {
#if NET8_0_OR_GREATER
            return Abi?.GetRawText();
#else
            if (Abi == null) return null;
            return Abi is string s ? s : JsonConvert.SerializeObject(Abi);
#endif
        }
    }
}
