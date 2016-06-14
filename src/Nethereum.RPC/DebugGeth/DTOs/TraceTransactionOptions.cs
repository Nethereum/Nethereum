using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.RPC.DebugGeth.DTOs
{
    public class TraceTransactionOptions
    {
        /// <summary>
        ///    Setting this to true will disable storage capture (default = false).
        /// </summary>
        [JsonProperty(PropertyName = "disableStorage")]
        public bool DisableStorage { get; set; }

        /// <summary>
        ///   Setting this to true will disable memory capture (default = false).
        /// </summary>
        [JsonProperty(PropertyName = "disableMemory")]
        public bool DisableMemory { get; set; }

        /// <summary>
        ///   Setting this to true will disable stack capture (default = false).
        /// </summary>
        [JsonProperty(PropertyName = "disableStack")]
        public bool DisableStack { get; set; }

        /// <summary>
        ///    Setting this to true will return you, for each opcode, the full storage, including everything which hasn't changed. This is a slow process and is therefor defaulted to false. By default it will only ever give you the changed storage values.
        /// </summary>
        [JsonProperty(PropertyName = "fullStorage")]
        public bool FullStorage { get; set; }
    }
}

  