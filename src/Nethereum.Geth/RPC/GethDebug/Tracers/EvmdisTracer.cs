using System;
using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Org.BouncyCastle.Math;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// evmdisTracer returns sufficient information from a trace to perform evmdis-style disassembly.
    /// </summary>
    /// 
    /// Return type: EvmdisTracerResponse

    public class EvmdisTracerTracer { }
    
    public class EvmdisTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "evmdisTracer"; 
        public override ITracerConfigDto TracerConfig { get; set; }
    }
    
    public class EvmdisTracerResponse : List<EvmdisTracerResponseItem>
    {
        
    }

    public class EvmdisTracerResponseItem
    {
        [JsonProperty("depth")]
        public long Depth { get; set; }
        
        [JsonProperty("len")]
        public long Len { get; set; }
        
        [JsonProperty("op")]
        public long Op { get; set; }
        
        [JsonProperty("result")]
        public List<string> Result { get; set; }
        
    }
    
    
}