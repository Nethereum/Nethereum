using Newtonsoft.Json;

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanResponse<T>
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }
    }
}
