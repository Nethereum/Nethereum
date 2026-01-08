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
    public class SourcifyCompilationInfo
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("language")]
#else
        [JsonProperty("language")]
#endif
        public string Language { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("compiler")]
#else
        [JsonProperty("compiler")]
#endif
        public string Compiler { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("compilerVersion")]
#else
        [JsonProperty("compilerVersion")]
#endif
        public string CompilerVersion { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("compilerSettings")]
        public JsonElement? CompilerSettings { get; set; }
#else
        [JsonProperty("compilerSettings")]
        public object CompilerSettings { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("contractName")]
#else
        [JsonProperty("contractName")]
#endif
        public string ContractName { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("fullyQualifiedName")]
#else
        [JsonProperty("fullyQualifiedName")]
#endif
        public string FullyQualifiedName { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("compilationTarget")]
#else
        [JsonProperty("compilationTarget")]
#endif
        public Dictionary<string, string> CompilationTarget { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sources")]
#else
        [JsonProperty("sources")]
#endif
        public Dictionary<string, SourcifyCompilationSource> Sources { get; set; }
    }

    public class SourcifyCompilationSource
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("keccak256")]
#else
        [JsonProperty("keccak256")]
#endif
        public string Keccak256 { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("urls")]
#else
        [JsonProperty("urls")]
#endif
        public List<string> Urls { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("license")]
#else
        [JsonProperty("license")]
#endif
        public string License { get; set; }
    }
}
