using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos
{
    public class DebugStorageAtResult
    {
        [JsonProperty("storage")]
        public Dictionary<string, AccountStorageValue> Storage { get; set; } = new Dictionary<string, AccountStorageValue>();

        [JsonProperty("nextKey")]
        public string NextKey { get; set; }
    }
}