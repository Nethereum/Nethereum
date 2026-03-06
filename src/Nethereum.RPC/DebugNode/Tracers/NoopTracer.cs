using Nethereum.RPC.DebugNode.Dtos.Tracing;

namespace Nethereum.RPC.DebugNode.Tracers
{
    public class NoopTracer { }

    public class NoopTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "noopTracer";
        public override ITracerConfigDto TracerConfig { get; set; }
    }

    public class NoopTracerConfigDto : TracerConfigDto<NoopTracer>
    {

    }

    public class NoopTracerResponse
    {

    }
}
