using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// trigramTracer counts the opcode trigrams. Trigrams are the possible combinations of three opcodes this tracer
    /// reports how many times each combination is seen during execution.
    /// </summary>
    /// 
    /// Return type: TrigramTracerResponse

    public class TrigramTracer { }
    
    public class TrigramTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "trigramTracer"; 
        public override ITracerConfigDto TracerConfig { get; set; }
    }
    
    public class TrigramTracerResponse : Dictionary<string, long>
    {
        
    }
    
    
}