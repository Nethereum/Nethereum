using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos
{
    public class AccountStorageValue
    {
        [JsonProperty("key")]
        public HexBigInteger Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}