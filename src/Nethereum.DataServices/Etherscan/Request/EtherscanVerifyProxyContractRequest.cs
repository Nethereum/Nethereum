#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Request
{
    public class EtherscanVerifyProxyContractRequest
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("address")]
#else
        [JsonProperty("address")]
#endif
        public string Address { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("expectedimplementation")]
#else
        [JsonProperty("expectedimplementation")]
#endif
        public string ExpectedImplementation { get; set; }
    }


}
