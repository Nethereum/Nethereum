using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TraceCallConfigDto : TraceConfigDto
    {
        /// <summary>
        /// Overrides for the state data (accounts/storage) for the call.
        /// </summary>
        [JsonProperty(PropertyName = "stateOverrides")]
        public Dictionary<string, StateOverrideDto> StateOverrides { get; set; }
        
        /// <summary>
        /// Overrides for the block data (number, timestamp etc) for the call.
        /// </summary>
        [JsonProperty(PropertyName = "blockOverrides")]
        public BlockOverridesDto BlockOverridesDto { get; set; }
        
        /// <summary>
        /// If set, the state at the given transaction index will be used to tracing
        /// </summary>
        [JsonProperty(PropertyName = "txIndex")]
        public HexBigInteger TxIndex { get; set; }
        
    }
}