using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos.Tracing
{
    public class TraceConfigDto
    {
        [JsonProperty(PropertyName = "tracer")]
        public string Tracer { get; set; }

        [JsonProperty(PropertyName = "timeout")]
        public string Timeout { get; set; }

        [JsonProperty(PropertyName = "tracerConfig")]
        public ITracerConfigDto TracerConfig { get; set; }

        [JsonProperty(PropertyName = "reexec")]
        public long? Reexec { get; set; }
    }
}
