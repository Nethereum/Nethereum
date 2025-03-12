using System;
using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Org.BouncyCastle.Math;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// The 4byteTracer collects the function selectors of every function executed in the lifetime of a transaction,
    /// along with the size of the supplied call data. The result is a map[string]int where the keys are
    /// SELECTOR-CALLDATASIZE and the values are number of occurrences of this key
    /// </summary>
    /// 
    /// Return type: FourByteResponse

    public class FourByteTracer { }
    
    public class FourByteTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "4byteTracer"; 
        public override ITracerConfigDto TracerConfig { get; set; }
    }

    public class FourByteTracerResponse : Dictionary<string, int>
    {
        
    }
    
    
}