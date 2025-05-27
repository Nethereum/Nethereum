using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.GethDebug.Tracers;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// This tracer is noop. It returns an empty object and is only meant for testing the setup.
    /// </summary>
    ///
    /// Return type: NoopTracerResponse

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