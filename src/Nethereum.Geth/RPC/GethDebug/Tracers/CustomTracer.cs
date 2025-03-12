using System;
using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.GethDebug.Tracers;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Org.BouncyCastle.Math;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// JavaScript based tracer for the custom tracing logic
    /// (See: https://geth.ethereum.org/docs/developers/evm-tracing/custom-tracer#custom-javascript-tracing)
    /// </summary>
    ///

    public class CustomTracer { }
        
    public class CustomTracerInfo : TracerInfo
    {
        public override string Tracer { get; }
        public override ITracerConfigDto TracerConfig { get; set; }

        
        public CustomTracerInfo(string tracerCode)
        {
            Tracer = "{" + tracerCode + "}";
        }
    }
    
}