using System;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    /// <summary>
    /// Builds the real executor stack (BlockExecutor engine + IncrementalStateRootCalculator
    /// + BlockImporter follower wrapper) used by integration tests.
    /// </summary>
    public static class FollowerStackBuilder
    {
        public static IBlockExecutor Build(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            Func<HardforkName, ChainConfig> chainConfigFactory)
        {
            var calculator = new IncrementalStateRootCalculator(bundle.State, bundle.TrieNodes);
            var engine = new BlockExecutor(
                bundle.State,
                bundle.Blocks,
                activations,
                chainConfigFactory,
                hardforkConfigFactory,
                calculator,
                EthereumProofOfWorkRewardPolicy.Instance,
                bundle.TrieNodes);
            return new BlockImporter(
                engine,
                bundle.Blocks,
                bundle.State,
                transactionStore: bundle.Transactions,
                receiptStore: bundle.Receipts,
                logStore: bundle.Logs,
                uncleStore: bundle.Uncles);
        }
    }
}
