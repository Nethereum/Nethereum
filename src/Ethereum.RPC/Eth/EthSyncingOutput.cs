using System;
using Newtonsoft.Json;

namespace Ethereum.RPC.Eth { 

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
            get { return StartingBlockHex.ConvertHexToNullableInt64(); }
        }

        public Int64? HighestBlock
        {
            get { return HighestBlockHex.ConvertHexToNullableInt64(); }
        }

        public Int64? CurrentBlock
        {
            get { return CurrentBlockHex.ConvertHexToNullableInt64(); }
        }

       
    }
}
