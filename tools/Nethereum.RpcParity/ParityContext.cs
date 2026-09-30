using System.Collections.Generic;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.RpcParity
{
    /// <summary>Shared state passed to every coverage group.</summary>
    public sealed class ParityContext
    {
        public ParityContext(Web3.Web3 nodeX, Web3.Web3 nodeY, List<long> blocks, ParityRunner runner)
        {
            NodeX = nodeX;
            NodeY = nodeY;
            Blocks = blocks;
            Runner = runner;
            ReferenceBlocks = new Dictionary<long, BlockWithTransactions>();
        }

        public Web3.Web3 NodeX { get; }
        public Web3.Web3 NodeY { get; }
        public List<long> Blocks { get; }
        public ParityRunner Runner { get; }

        /// <summary>
        /// Blocks-with-transactions fetched once up front from node Y (the
        /// reference node), so every group derives the same first-tx hash /
        /// block hash / busy address without a redundant fetch per group.
        /// </summary>
        public Dictionary<long, BlockWithTransactions> ReferenceBlocks { get; }
    }
}
