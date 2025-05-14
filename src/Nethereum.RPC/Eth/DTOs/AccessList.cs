using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccessList
    {
        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }
        [JsonProperty(PropertyName = "storageKeys")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("storageKeys")]
#endif
        public List<string> StorageKeys { get; set; }
    }
}