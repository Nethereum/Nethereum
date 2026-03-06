using System.Collections.Generic;
using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.DebugNode.Dtos.Tracing
{
    public class TracingCallOptions : TracingOptions
    {
        public Dictionary<string, StateOverrideDto> StateOverrides { get; set; }
        public BlockOverridesDto BlockOverridesDto { get; set; }
        public HexBigInteger TxIndex { get; set; }

        public new TraceCallConfigDto ToDto()
        {
            var baseDto = base.ToDto();

            return new TraceCallConfigDto
            {
                Timeout = baseDto.Timeout,
                Reexec = baseDto.Reexec,
                Tracer = baseDto.Tracer,
                TracerConfig = baseDto.TracerConfig,
                StateOverrides = StateOverrides ?? null,
                BlockOverridesDto = BlockOverridesDto ?? null,
                TxIndex = TxIndex ?? null
            };
        }
    }
}
