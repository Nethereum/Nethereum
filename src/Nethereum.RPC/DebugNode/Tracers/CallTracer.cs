using System.Collections.Generic;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Tracers
{
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
        [JsonProperty("onlyTopCall")]
        public bool OnlyTopCall { get; set; } = false;

        [JsonProperty("withLog")]
        public bool WithLog { get; set; } = false;
    }

    public class CallTracerResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("value")]
        public HexBigInteger Value { get; set; }

        [JsonProperty("gas")]
        public HexBigInteger Gas { get; set; }

        [JsonProperty("gasUsed")]
        public HexBigInteger GasUsed { get; set; }

        [JsonProperty("input")]
        public string Input { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("revertReason")]
        public string RevertReason { get; set; }

        [JsonProperty("calls")]
        public List<CallTracerResponse> Calls { get; set; }

        [JsonProperty("logs")]
        public List<TracerLogDto> Logs { get; set; }
    }
}
