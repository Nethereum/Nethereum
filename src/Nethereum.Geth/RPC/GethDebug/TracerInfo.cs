using Nethereum.Geth.RPC.GethDebug.Tracers;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public abstract class TracerInfo
    {
        public abstract string Tracer { get; }
        public abstract ITracerConfigDto TracerConfig { get; set; }

    }
}