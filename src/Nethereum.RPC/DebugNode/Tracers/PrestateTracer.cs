using System.Collections.Generic;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Tracers
{
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

    public class PrestateTracerConfigDto : TracerConfigDto<PrestateTracer>
    {
        [JsonProperty("diffMode")]
        public bool DiffMode { get; set; } = false;
    }

    public class PrestateTracerResponseDiffMode
    {
        [JsonProperty("pre")]
        public Dictionary<string, PrestateTracerResponseItem> Pre { get; set; }

        [JsonProperty("post")]
        public Dictionary<string, PrestateTracerResponseItem> Post { get; set; }
    }

    public class PrestateTracerResponsePrestateMode : Dictionary<string, PrestateTracerResponseItem>
    {

    }

    public class PrestateTracerResponseItem
    {
        [JsonProperty("balance")]
        public HexBigInteger Balance { get; set; }

        [JsonProperty("nonce")]
        public long Nonce { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; }
    }
}
