using Nethereum.ABI.CompilationMetadata;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;

namespace Nethereum.DataServices.Etherscan.Responses
{

    public class EtherscanGetSourceCodeResponse
    {
        [JsonProperty("SourceCode")]
        public string SourceCode { get; set; }

        [JsonProperty("ABI")]
        public string ABI { get; set; }

        [JsonProperty("ContractName")]
        public string ContractName { get; set; }

        [JsonProperty("CompilerVersion")]
        public string CompilerVersion { get; set; }

        [JsonProperty("OptimizationUsed")]
        public string OptimizationUsed { get; set; }

        [JsonProperty("Runs")]
        public string Runs { get; set; }

        [JsonProperty("ConstructorArguments")]
        public string ConstructorArguments { get; set; }

        [JsonProperty("EVMVersion")]
        public string EVMVersion { get; set; }

        [JsonProperty("Library")]
        public string Library { get; set; }

        [JsonProperty("LicenseType")]
        public string LicenseType { get; set; }

        [JsonProperty("Proxy")]
        public string Proxy { get; set; }

        [JsonProperty("Implementation")]
        public string Implementation { get; set; }

        [JsonProperty("SwarmSource")]
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

                return JsonConvert.DeserializeObject<CompilationMetadata>(source);
            }
            //TODO: Convert model to CompilationMetadata
            return null;
        }
    }

    public static class EtherscanCompilationMetadataExtensions
    {
        public static SourceCode GetLocalSourceCode(this CompilationMetadata compilationMetadata, string contractPathName)
        {
            if (compilationMetadata.Language == "Solidity" && !contractPathName.EndsWith(".sol")) contractPathName = contractPathName + ".sol";
            return compilationMetadata.Sources["contracts/" + contractPathName];
        }
    }
    
}
