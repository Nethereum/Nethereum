using Newtonsoft.Json;

namespace Nethereum.Quorum.Enclave
{
    public class StoreRawResponse
    {
        [JsonProperty(PropertyName = "key")]
        public string Key { get; set; }
    }
}