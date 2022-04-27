using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TraceCallOptions
    {
        /// <summary>
        /// Setting this will enable JavaScript-based transaction tracing.
        /// </summary>
        [JsonProperty(PropertyName = "tracer")]
        public string Tracer { get; set; }

        /// <summary>
        /// Overrides the default timeout of 5 seconds for JavaScript-based tracing calls. Valid values are described here:
        /// https://golang.org/pkg/time/#ParseDuration
        /// </summary>
        [JsonProperty(PropertyName = "timeout")]
        public string Timeout { get; set; }
    }
}
