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
    /// The callTracer tracks all the call frames executed during a transaction, including depth 0.
    /// The result will be a nested list of call frames, resembling how EVM works.
    /// They form a tree with the top-level call at root and sub-calls as children of the higher levels.
    /// </summary>
    ///
    /// Return type: CallTracerResponse

    public class CallTracer { }
        
    public class CallTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = "callTracer";
        public override ITracerConfigDto TracerConfig { get; set; }

        public CallTracerInfo(bool? onlyTopCall, bool? withLog)
        {
            if (onlyTopCall != null || withLog != null)
            {
                TracerConfig = new CallTracerConfigDto
                {
                    OnlyTopCall = onlyTopCall ?? default, 
                    WithLog = withLog ?? default
                };
            }
            
        }
    }
    
    
    public class CallTracerConfigDto : TracerConfigDto<CallTracer>
    {
         
        /// <summary>
        /// Instructs the tracer to only process the main (top-level) call and none of the sub-calls.
        /// This avoids extra processing for each call frame if only the top-level call info are required.
        /// </summary>
        [JsonProperty("onlyTopCall")]
        public bool OnlyTopCall { get; set; } = false;

        /// <summary>
        /// Instructs the tracer to also collect the logs emitted during each call.
        /// </summary>
        [JsonProperty("withLog")]
        public bool WithLog { get; set; } = false;

    }
    
    
    public class CallTracerResponse
    {
        /// <summary>
        /// CALL, STATICCALL or CREATE
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// From address
        /// </summary>
        [JsonProperty("from")]
        public string From { get; set; }

        /// <summary>
        /// To address
        /// </summary>
        [JsonProperty("to")]
        public string To { get; set; }

        /// <summary>
        /// Hex-encoded amount of value transfer
        /// </summary>
        [JsonProperty("value")]
        public HexBigInteger Value { get; set; }

        /// <summary>
        /// Hex-encoded gas provided for call
        /// </summary>
        [JsonProperty("gas")]
        public HexBigInteger Gas { get; set; }

        /// <summary>
        /// Hex-encoded gas used during call
        /// </summary>
        [JsonProperty("gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        /// <summary>
        /// Call data
        /// </summary>
        [JsonProperty("input")]
        public string Input { get; set; }

        /// <summary>
        /// Return data
        /// </summary>
        [JsonProperty("output")]
        public string Output { get; set; }
        
        /// <summary>
        /// Error, if any
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }
        
        /// <summary>
        /// Solidity revert reason, if any
        /// </summary>
        [JsonProperty("revertReason")]
        public string RevertReason { get; set; }

        /// <summary>
        /// Inner calls
        /// </summary>
        [JsonProperty("calls")]
        public List<CallTracerResponse> Calls { get; set; }
        
        /// <summary>
        /// Emitted logs
        /// </summary>
        [JsonProperty("logs")]
        public List<TracerLogDto> Logs { get; set; }
        
    }
    
}