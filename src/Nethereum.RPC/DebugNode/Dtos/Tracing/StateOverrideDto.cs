using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos.Tracing
{
    public class StateOverrideDto
    {
        [JsonProperty("balance")]
        public HexBigInteger Balance { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("state")]
        public Dictionary<string, string> State { get; set; }

        [JsonProperty("stateDiff")]
        public Dictionary<string, string> StateDiff { get; set; }

        [JsonProperty("movePrecompileToAddress")]
        public string MovePrecompileToAddress { get; set; }
    }
}
