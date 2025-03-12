using System;
using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Org.BouncyCastle.Math;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// The bigramTracer counts the opcode bigrams, i.e. how many times 2 opcodes were executed one after the other.
    /// </summary>
    /// 
    /// Return type: BigramResponse

    public class BigramTracer { }
    
    public class BigramTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "bigramTracer"; 
        public override ITracerConfigDto TracerConfig { get; set; }
    }

    public class BigramTracerResponse : Dictionary<string, long>
    {
        
    }
    
    
}