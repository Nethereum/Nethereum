using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TraceTransactionOptions
    {
        /// <summary>
        ///     Setting this to true will disable storage capture (default = false).
        /// </summary>
        [JsonProperty(PropertyName = "disableStorage")]
        public bool DisableStorage { get; set; }

        /// <summary>
        ///     Setting this to true will disable memory capture (default = false).
        /// </summary>
        [JsonProperty(PropertyName = "disableMemory")]
        public bool DisableMemory { get; set; }

        /// <summary>
        ///     Setting this to true will disable stack capture (default = false).
        /// </summary>
        [JsonProperty(PropertyName = "disableStack")]
        public bool DisableStack { get; set; }

        /// <summary>
        ///     Setting this to true will return you, for each opcode, the full storage, including everything which hasn't changed.
        ///     This is a slow process and is therefor defaulted to false. By default it will only ever give you the changed
        ///     storage values.
        /// </summary>
        [JsonProperty(PropertyName = "fullStorage")]
        public bool FullStorage { get; set; }
        
        /// <summary>
        /// Setting this will enable JavaScript-based transaction tracing, described below.
        /// If set, the previous four arguments will be ignored.
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