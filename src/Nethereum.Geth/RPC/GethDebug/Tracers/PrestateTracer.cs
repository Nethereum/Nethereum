using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.GethDebug.Tracers;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// The prestate tracer has two modes: prestate and diff. The prestate mode returns the accounts necessary to
    /// execute a given transaction. diff mode returns the differences between the transaction's pre and post-state
    /// (i.e. what changed because the transaction happened).
    /// </summary>
    ///
    ///
    /// Return type:
    ///     In prestate mode: PrestateTracerResponsePrestateMode
    ///     In diff mode: PrestateTracerResponseDiffMode 

    public class PrestateTracer { }
        
    public class PrestateTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "prestateTracer";
        public override ITracerConfigDto TracerConfig { get; set; }

        public PrestateTracerInfo(bool? diffMode)
        {
            if (diffMode != null)
            {
                TracerConfig = new PrestateTracerConfigDto
                {
                    DiffMode = diffMode ?? default, 
                };
            }
            
        }
    }
    
    public class PrestateTracerConfigDto : TracerConfigDto<CallTracer>
    {
         
        /// <summary>
        /// Mode returns the differences between the transaction's pre and post-state
        /// (i.e. what changed because the transaction happened)
        /// </summary>
        [JsonProperty("diffMode")]
        public bool DiffMode { get; set; } = false;
        
    }


    public class PrestateTracerResponseDiffMode
    {
        /// <summary>
        /// Pre state info
        /// </summary>
        [JsonProperty("pre")]
        public Dictionary<string, PrestateTracerResponseItem> Pre { get; set; }

        /// <summary>
        /// Post state info
        /// </summary>
        [JsonProperty("post")]
        public Dictionary<string, PrestateTracerResponseItem> Post { get; set; }
    }
    
    public class PrestateTracerResponsePrestateMode : Dictionary<string, PrestateTracerResponseItem>
    {
        
    }
    
    public class PrestateTracerResponseItem
    {
        /// <summary>
        /// Balance in Wei
        /// </summary>
        [JsonProperty("balance")]
        public HexBigInteger Balance { get; set; }

        /// <summary>
        /// Nonce
        /// </summary>
        [JsonProperty("nonce")]
        public long Nonce { get; set; }

        /// <summary>
        /// Hex-encoded bytecode
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        /// <summary>
        /// Storage slots of the contract
        /// </summary>
        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; }
        
    }
    
    
}