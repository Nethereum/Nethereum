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
    public class SourcifyBytecodeInfo
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("onchainBytecode")]
#else
        [JsonProperty("onchainBytecode")]
#endif
        public string OnchainBytecode { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("recompiledBytecode")]
#else
        [JsonProperty("recompiledBytecode")]
#endif
        public string RecompiledBytecode { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("sourceMap")]
#else
        [JsonProperty("sourceMap")]
#endif
        public string SourceMap { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("linkReferences")]
        public JsonElement? LinkReferences { get; set; }
#else
        [JsonProperty("linkReferences")]
        public object LinkReferences { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("cborAuxdata")]
        public JsonElement? CborAuxdata { get; set; }
#else
        [JsonProperty("cborAuxdata")]
        public object CborAuxdata { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("immutableReferences")]
        public JsonElement? ImmutableReferences { get; set; }
#else
        [JsonProperty("immutableReferences")]
        public object ImmutableReferences { get; set; }
#endif

#if NET8_0_OR_GREATER
        [JsonPropertyName("transformations")]
#else
        [JsonProperty("transformations")]
#endif
        public List<SourcifyBytecodeTransformation> Transformations { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("transformationValues")]
#else
        [JsonProperty("transformationValues")]
#endif
        public SourcifyTransformationValues TransformationValues { get; set; }
    }

    public class SourcifyBytecodeTransformation
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("id")]
#else
        [JsonProperty("id")]
#endif
        public string Id { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("type")]
#else
        [JsonProperty("type")]
#endif
        public string Type { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("offset")]
#else
        [JsonProperty("offset")]
#endif
        public int Offset { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("reason")]
#else
        [JsonProperty("reason")]
#endif
        public string Reason { get; set; }
    }

    public class SourcifyTransformationValues
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("libraries")]
#else
        [JsonProperty("libraries")]
#endif
        public Dictionary<string, string> Libraries { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("constructorArguments")]
#else
        [JsonProperty("constructorArguments")]
#endif
        public string ConstructorArguments { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("immutables")]
#else
        [JsonProperty("immutables")]
#endif
        public Dictionary<string, string> Immutables { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("cborAuxdata")]
#else
        [JsonProperty("cborAuxdata")]
#endif
        public Dictionary<string, string> CborAuxdata { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("callProtection")]
#else
        [JsonProperty("callProtection")]
#endif
        public string CallProtection { get; set; }
    }
}
