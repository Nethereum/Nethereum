using System.Collections.Generic;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.GethDebug.Tracers;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.Tracers
{
    /// <summary>
    /// The struct logger (aka opcode logger) is a native Go tracer which executes a transaction and emits the opcode
    /// and execution context at every step
    /// If no tracer is specified the opcode tracer will be used
    /// </summary>
    ///
    /// Return type: OpcodeTracerResponse
    /// 
    public class OpcodeTracer { }

    public class OpcodeTracerInfo : TracerInfo
    {
        public override string Tracer { get; } = null;
        public override ITracerConfigDto TracerConfig { get; set; }

        public OpcodeTracerInfo(
            bool? enableMemory = false, 
            bool? disableStack = false, 
            bool? disableStorage = false, 
            bool? enableReturnData = false,
            bool? debug = false,
            int? limit = 0)
        {
            if (enableMemory != null || 
                disableStack != null ||
                disableStorage != null ||
                enableReturnData != null ||
                debug != null ||
                limit != null)
            {
                TracerConfig = new OpcodeTracerConfigDto()
                {
                    EnableMemory = enableMemory ?? default, 
                    DisableStack = disableStack ?? default, 
                    DisableStorage = disableStorage ?? default, 
                    EnableReturnData = enableReturnData ?? default, 
                    Debug = debug ?? default, 
                    Limit = limit ?? default, 
                };
            }
        }
        
    }


    public class OpcodeTracerConfigDto : TracerConfigDto<OpcodeTracer>
    {
        /// <summary>
        /// Setting this to true will enable memory capture
        /// </summary>
        [JsonProperty("enableMemory")]
        public bool EnableMemory { get; set; } = false;

        /// <summary>
        /// Setting this to true will disable stack capture
        /// </summary>
        [JsonProperty("disableStack")]
        public bool DisableStack { get; set; } = false;

        /// <summary>
        /// Setting this to true will disable storage capture
        /// </summary>
        [JsonProperty("disableStorage")]
        public bool DisableStorage { get; set; } = false;

        /// <summary>
        /// Setting this to true will enable return data capture
        /// </summary>
        [JsonProperty("enableReturnData")]
        public bool EnableReturnData { get; set; } = false;

        /// <summary>
        /// Setting this to true will print output during capture end
        /// </summary>
        [JsonProperty("debug")]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Setting this to a positive integer will limit the number of steps captured (default = 0, no limit).
        /// </summary>
        [JsonProperty("limit")]
        public int Limit { get; set; } = 0;
    }


    
    public class OpcodeTracerResponse
    {
        [JsonProperty("gas")]
        public ulong Gas { get; set; }
        
        [JsonProperty("failed")]
        public bool Failed { get; set; }

        /// <summary>
        /// Last call's return data. Enabled via enableReturnData
        /// </summary>
        [JsonProperty("returnValue")]
        public string ReturnValue { get; set; }
        
        /// <summary>
        /// Struct logs
        /// </summary>
        [JsonProperty("structLogs")]
        public List<StructLog> StructLogs { get; set; }

        
        
    }

    public class StructLog
    {
        /// <summary>
        /// Program counter
        /// </summary>
        [JsonProperty("pc")]
        public ulong Pc { get; set; }

        /// <summary>
        /// Opcode to be executed
        /// </summary>
        [JsonProperty("op")]
        public string Op { get; set; }

        /// <summary>
        /// Remaining gas
        /// </summary>
        [JsonProperty("gas")]
        public ulong Gas { get; set; }

        /// <summary>
        /// Cost for executing op
        /// </summary>
        [JsonProperty("gasCost")]
        public ulong GasCost { get; set; }

        /// <summary>
        /// EVM memory. Enabled via enableMemory
        /// </summary>
        [JsonProperty("memory")]
        public string Memory { get; set; }

        /// <summary>
        /// Size of memory
        /// </summary>
        [JsonProperty("memSize")]
        public int MemSize { get; set; }

        /// <summary>
        /// EVM stack. Disabled via disableStack
        /// </summary>
        [JsonProperty("stack")]
        public List<HexBigInteger> Stack { get; set; }


        /// <summary>
        /// Storage slots of current contract read from and written to. Only emitted for SLOAD and SSTORE. Disabled via disableStorage
        /// </summary>
        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; }

        /// <summary>
        /// Current call depth
        /// </summary>
        [JsonProperty("depth")]
        public int Depth { get; set; }

        /// <summary>
        /// Refund counter
        /// </summary>
        [JsonProperty("refund")]
        public ulong Refund { get; set; }

        /// <summary>
        /// Error message if any
        /// </summary>
        [JsonProperty("error")]
        public string Error { get; set; }

    }
    
}