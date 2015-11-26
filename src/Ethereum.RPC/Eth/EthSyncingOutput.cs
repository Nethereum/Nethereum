using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace Ethereum.RPC { 

    /// <summary>
    /// Object - The transaction object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EthSyncingOutput
    {
        /// <summary>
        /// Is it synching?
        /// </summary>
        public bool Synching { get; set; }
        /// <summary>
        ///  StartingBlock: QUANTITY - The block at which the import started (will only be reset, after the sync reached his head)
        /// </summary>
        [JsonProperty(PropertyName = "startingBlock")]
        public string StartingBlockHex { get; set; }

        /// <summary>
        /// CurrentBlock: QUANTITY - The current block, same as eth_blockNumber
        /// </summary>
        [JsonProperty(PropertyName = "startingBlock")]
        public string  CurrentBlockHex { get; set; }

        /// <summary>
        /// HighestBlock: QUANTITY - The estimated highest block
        /// </summary>
        public string HighestBlockHex { get; set; }

        public Int64? StartingBlock
        {
            get { return ConvertToHex(StartingBlockHex); }
        }

        public Int64? HighestBlock
        {
            get { return ConvertToHex(HighestBlockHex); }
        }

        public Int64? CurrentBlock
        {
            get { return ConvertToHex(CurrentBlockHex); }
        }

        private Int64? ConvertToHex(string input)
        {
            if (input == null) return null;
            return input.ConvertHexToInt64();
        }
    }
}
