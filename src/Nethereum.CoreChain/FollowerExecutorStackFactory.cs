using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;

namespace Nethereum.CoreChain
{
    public static class FollowerExecutorStackFactory
    {
        public readonly struct FollowerExecutorStack
        {
            public FollowerExecutorStack(IBlockExecutor executor, IncrementalStateRootCalculator calculator)
            {
                Executor = executor;
                Calculator = calculator;
            }

            public IBlockExecutor Executor { get; }
            public IncrementalStateRootCalculator Calculator { get; }
        }

        public static IBlockExecutor BuildFollowerExecutorStack(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IRewardPolicy rewardPolicy,
            ILoggerFactory? loggerFactory = null,
            IStateStore? stateOverride = null,
            IFlushCadence? flushCadence = null)
            => BuildFollowerExecutorStackWithCalculator(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                rewardPolicy, loggerFactory, stateOverride, flushCadence).Executor;

        public static FollowerExecutorStack BuildFollowerExecutorStackWithCalculator(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IRewardPolicy rewardPolicy,
            ILoggerFactory? loggerFactory = null,
            IStateStore? stateOverride = null,
            IFlushCadence? flushCadence = null)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (activations == null) throw new ArgumentNullException(nameof(activations));
            if (chainConfigFactory == null) throw new ArgumentNullException(nameof(chainConfigFactory));
            if (hardforkConfigFactory == null) throw new ArgumentNullException(nameof(hardforkConfigFactory));
            if (rewardPolicy == null) throw new ArgumentNullException(nameof(rewardPolicy));

            var state = stateOverride ?? bundle.State;
            var effectiveLoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;

            var calc = new IncrementalStateRootCalculator(state, bundle.StateTrieNodes,
                emitTombstones: !ReferenceEquals(bundle.StateTrieNodes, bundle.TrieNodes));
            var engine = new BlockExecutor(
                state, bundle.Blocks, activations,
                chainConfigFactory: chainConfigFactory,
                hardforkConfigFactory: hardforkConfigFactory,
                stateRootCalculator: calc,
                rewardPolicy: rewardPolicy,
                trieNodeStore: bundle.TrieNodes,
                logger: effectiveLoggerFactory.CreateLogger<BlockExecutor>());

            var importer = new BlockImporter(
                engine, bundle.Blocks, state,
                bundle.Transactions, bundle.Receipts, bundle.Logs, bundle.Uncles,
                logger: effectiveLoggerFactory.CreateLogger<BlockImporter>(),
                nodeCommitBlockContext: bundle.NodeCommitBlockSource,
                atomicFlush: bundle as IAtomicBlockFlush,
                flushCadence: flushCadence,
                blockAccessListStore: bundle.BlockAccessLists,
                withdrawalStore: bundle.Withdrawals);

            return new FollowerExecutorStack(importer, calc);
        }
    }
}
