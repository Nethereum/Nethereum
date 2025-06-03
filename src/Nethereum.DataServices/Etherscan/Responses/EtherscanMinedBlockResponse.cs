#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanMinedBlockResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("blockNumber")]
#else
        [JsonProperty("blockNumber")]
#endif
        public string BlockNumber { get; set; }

#if NET8_0_OR_GREATER
    [JsonPropertyName("timeStamp")]
#else
        [JsonProperty("timeStamp")]
#endif
        public string TimeStamp { get; set; }
    }
}