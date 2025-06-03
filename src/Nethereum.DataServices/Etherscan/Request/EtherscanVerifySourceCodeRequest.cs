using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Request
{
    public class EtherscanVerifySourceCodeRequest
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("chainId")]
#else
        [JsonProperty("chainId")]
#endif
        public string ChainId { get; set; } = "1";

#if NET8_0_OR_GREATER
    [JsonPropertyName("codeformat")]
#else
        [JsonProperty("codeformat")]
#endif
        public string CodeFormat { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("sourceCode")]
#else
        [JsonProperty("sourceCode")]
#endif
        public string SourceCode { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("contractaddress")]
#else
        [JsonProperty("contractaddress")]
#endif
        public string ContractAddress { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("contractname")]
#else
        [JsonProperty("contractname")]
#endif
        public string ContractName { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("compilerversion")]
#else
        [JsonProperty("compilerversion")]
#endif
        public string CompilerVersion { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("constructorArguments")]
#else
        [JsonProperty("constructorArguments")]
#endif
        public string ConstructorArguments { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("optimizationUsed")]
#else
        [JsonProperty("optimizationUsed")]
#endif
        public string OptimizationUsed { get; set; }
    }


}
