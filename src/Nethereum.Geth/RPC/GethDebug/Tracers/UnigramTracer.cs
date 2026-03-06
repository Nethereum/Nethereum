using System.Collections.Generic;
using Nethereum.RPC.DebugNode.Dtos.Tracing;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// unigramTracer counts the frequency of occurrence of each opcode.
    /// </summary>
    /// 
    /// Return type: UnigramTracerResponse

    public class UnigramTracer { }
    
    public class UnigramTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "unigramTracer"; 
        public override ITracerConfigDto TracerConfig { get; set; }
    }

    public class UnigramTracerResponse : Dictionary<string, long>
    {
        
    }

    
    
}