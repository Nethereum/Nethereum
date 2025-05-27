using Nethereum.Geth.RPC.Debug.DTOs;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// opcountTracer counts the total number of opcodes executed and simply returns the number.
    /// </summary>
    /// 
    /// Return type: long

    public class OpcountTracer { }
    
    public class OpcountTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "opcountTracer"; 
        public override ITracerConfigDto TracerConfig { get; set; }
    }
    
    
}