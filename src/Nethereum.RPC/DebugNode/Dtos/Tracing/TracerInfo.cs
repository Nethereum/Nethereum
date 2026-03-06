namespace Nethereum.RPC.DebugNode.Dtos.Tracing
{
    public abstract class TracerInfo
    {
        public abstract string Tracer { get; }
        public abstract ITracerConfigDto TracerConfig { get; set; }

    }
}
