using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    public class TraceTransactionOptions
    {
        /// <summary>
        ///     Setting this to true will disable storage capture (default = false).
        /// </summary>
        [DataMember(Name="disableStorage")]
        public bool DisableStorage { get; set; }

        /// <summary>
        ///     Setting this to true will disable memory capture (default = false).
        /// </summary>
        [DataMember(Name = "disableMemory")]
        public bool DisableMemory { get; set; }

        /// <summary>
        ///     Setting this to true will disable stack capture (default = false).
        /// </summary>
        [DataMember(Name = "disableStack")]
        public bool DisableStack { get; set; }

        /// <summary>
        ///     Setting this to true will return you, for each opcode, the full storage, including everything which hasn't changed.
        ///     This is a slow process and is therefor defaulted to false. By default it will only ever give you the changed
        ///     storage values.
        /// </summary>
        [DataMember(Name = "fullStorage")]
        public bool FullStorage { get; set; }
    }
}