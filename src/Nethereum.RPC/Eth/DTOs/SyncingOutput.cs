using Nethereum.Hex.HexTypes;

namespace Nethereum.RPC.Eth.DTOs {
    
    /// <summary>
    /// Object - The transaction object
    /// </summary>
    
    public class SyncingOutput
    {
        /// <summary>
        /// Is it synching?
        /// </summary>
        
        public bool Synching { get; set; }
        /// <summary>
        ///  StartingBlock: QUANTITY - The block at which the import started (will only be reset, after the sync reached his head)
        /// </summary>
        
        public HexBigInteger StartingBlock { get; set; }

        /// <summary>
        /// CurrentBlock: QUANTITY - The current block, same as eth_blockNumber
        /// </summary>
        
        public HexBigInteger CurrentBlock { get; set; }

        /// <summary>
        /// HighestBlock: QUANTITY - The estimated highest block
        /// </summary>
        
        public HexBigInteger HighestBlock { get; set; }

    }
}
