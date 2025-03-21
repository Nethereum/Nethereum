using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TraceConfigDto
    {
        /// <summary>
        /// Name for built-in tracer or Javascript expression.
        /// </summary>
        [JsonProperty(PropertyName = "tracer")]
        public string Tracer { get; set; }

        /// <summary>
        /// Overrides the default timeout of 5 seconds for JavaScript-based tracing calls. Valid values are described here:
        /// https://golang.org/pkg/time/#ParseDuration
        /// </summary>
        [JsonProperty(PropertyName = "timeout")]
        public string Timeout { get; set; }
        
        /// <summary>
        /// Config for the specified tracer formatted as a JSON object
        /// </summary>
        [JsonProperty(PropertyName = "tracerConfig")]
        public ITracerConfigDto TracerConfig { get; set; }
        
        /// <summary>
        /// The number of blocks the tracer is willing to go back and re-execute to produce missing historical state
        /// necessary to run a specific trace.
        /// </summary>
        [JsonProperty(PropertyName = "reexec")]
        public long? Reexec { get; set; }
    }

    
}