using System.Collections.Generic;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Tracers
{
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
        [JsonProperty("enableMemory")]
        public bool EnableMemory { get; set; } = false;

        [JsonProperty("disableStack")]
        public bool DisableStack { get; set; } = false;

        [JsonProperty("disableStorage")]
        public bool DisableStorage { get; set; } = false;

        [JsonProperty("enableReturnData")]
        public bool EnableReturnData { get; set; } = false;

        [JsonProperty("debug")]
        public bool Debug { get; set; } = false;

        [JsonProperty("limit")]
        public int Limit { get; set; } = 0;
    }

    public class OpcodeTracerResponse
    {
        [JsonProperty("gas")]
        public ulong Gas { get; set; }

        [JsonProperty("failed")]
        public bool Failed { get; set; }

        [JsonProperty("returnValue")]
        public string ReturnValue { get; set; }

        [JsonProperty("structLogs")]
        public List<StructLog> StructLogs { get; set; }
    }

    public class StructLog
    {
        [JsonProperty("pc")]
        public ulong Pc { get; set; }

        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("gas")]
        public ulong Gas { get; set; }

        [JsonProperty("gasCost")]
        public ulong GasCost { get; set; }

        [JsonProperty("memory")]
        public string Memory { get; set; }

        [JsonProperty("memSize")]
        public int MemSize { get; set; }

        [JsonProperty("stack")]
        public List<HexBigInteger> Stack { get; set; }

        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; }

        [JsonProperty("depth")]
        public int Depth { get; set; }

        [JsonProperty("refund")]
        public ulong Refund { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
