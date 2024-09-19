using System;
#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

using Nethereum.ABI.CompilationMetadata;

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanGetSourceCodeResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("SourceCode")]
#else
        [JsonProperty("SourceCode")]
#endif
        public string SourceCode { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("ABI")]
#else
        [JsonProperty("ABI")]
#endif
        public string ABI { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("ContractName")]
#else
        [JsonProperty("ContractName")]
#endif
        public string ContractName { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("CompilerVersion")]
#else
        [JsonProperty("CompilerVersion")]
#endif
        public string CompilerVersion { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("OptimizationUsed")]
#else
        [JsonProperty("OptimizationUsed")]
#endif
        public string OptimizationUsed { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("Runs")]
#else
        [JsonProperty("Runs")]
#endif
        public string Runs { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("ConstructorArguments")]
#else
        [JsonProperty("ConstructorArguments")]
#endif
        public string ConstructorArguments { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("EVMVersion")]
#else
        [JsonProperty("EVMVersion")]
#endif
        public string EVMVersion { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("Library")]
#else
        [JsonProperty("Library")]
#endif
        public string Library { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("LicenseType")]
#else
        [JsonProperty("LicenseType")]
#endif
        public string LicenseType { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("Proxy")]
#else
        [JsonProperty("Proxy")]
#endif
        public string Proxy { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("Implementation")]
#else
        [JsonProperty("Implementation")]
#endif
        public string Implementation { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("SwarmSource")]
#else
        [JsonProperty("SwarmSource")]
#endif
        public string SwarmSource { get; set; }

 
        public bool ContainsSourceCodeCompilationMetadata()
        {
            return !string.IsNullOrEmpty(SourceCode) && SourceCode.TrimStart().StartsWith("{{");
        }

  
        public CompilationMetadata DeserialiseCompilationMetadata()
        {
            if (ContainsSourceCodeCompilationMetadata())
            {
                var source = SourceCode.Trim().Substring(1, SourceCode.Length - 2);

                return CompilationMetadataDeserialiser.DeserialiseCompilationMetadata(source);
            }
            
            return null;
        }
    }

    public static class EtherscanCompilationMetadataExtensions
    {
        public static SourceCode GetLocalSourceCode(this CompilationMetadata compilationMetadata, string contractPathName)
        {
            if (compilationMetadata.Language == "Solidity" && !contractPathName.EndsWith(".sol"))
                contractPathName = contractPathName + ".sol";

            return compilationMetadata.Sources["contracts/" + contractPathName];
        }
    }
}
