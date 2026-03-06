using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos.Tracing
{
    public class TraceCallConfigDto : TraceConfigDto
    {
        [JsonProperty(PropertyName = "stateOverrides")]
        public Dictionary<string, StateOverrideDto> StateOverrides { get; set; }

        [JsonProperty(PropertyName = "blockOverrides")]
        public BlockOverridesDto BlockOverridesDto { get; set; }

        [JsonProperty(PropertyName = "txIndex")]
        public HexBigInteger TxIndex { get; set; }
    }
}
