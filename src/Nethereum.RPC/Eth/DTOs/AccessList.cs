using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccessList
    {
        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }
        [JsonProperty(PropertyName = "storageKeys")]
        public List<string> StorageKeys { get; set; }
    }
}