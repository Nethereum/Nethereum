using System;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Prefetch;
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
            ILoggerFactory? loggerFactory,
            IStateStore? stateOverride,
            IFlushCadence? flushCadence,
            StatePrefetcher? prefetcher)
            => BuildFollowerExecutorStackWithCalculator(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                rewardPolicy, loggerFactory, stateOverride, flushCadence, prefetcher).Executor;

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
            => BuildFollowerExecutorStackWithCalculator(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                rewardPolicy, loggerFactory, stateOverride, flushCadence, prefetcher: null);

        public static FollowerExecutorStack BuildFollowerExecutorStackWithCalculator(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IRewardPolicy rewardPolicy,
            ILoggerFactory? loggerFactory,
            IStateStore? stateOverride,
            IFlushCadence? flushCadence,
            StatePrefetcher? prefetcher)
            => BuildFollowerExecutorStackWithCalculator(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                rewardPolicy, loggerFactory, stateOverride, flushCadence, prefetcher, ExecutionStackOptions.DefaultStateRootWorkers);

        public static FollowerExecutorStack BuildFollowerExecutorStackWithCalculator(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IRewardPolicy rewardPolicy,
            ILoggerFactory? loggerFactory,
            IStateStore? stateOverride,
            IFlushCadence? flushCadence,
            StatePrefetcher? prefetcher,
            int stateRootWorkers)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            if (activations == null) throw new ArgumentNullException(nameof(activations));
            if (chainConfigFactory == null) throw new ArgumentNullException(nameof(chainConfigFactory));
            if (hardforkConfigFactory == null) throw new ArgumentNullException(nameof(hardforkConfigFactory));
            if (rewardPolicy == null) throw new ArgumentNullException(nameof(rewardPolicy));

            var stack = ExecutionStackFactory.Build(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                new ExecutionStackOptions
                {
                    RewardPolicy = rewardPolicy,
                    LoggerFactory = loggerFactory,
                    StateOverride = stateOverride,
                    FlushCadence = flushCadence,
                    Prefetcher = prefetcher,
                    StateRootWorkers = stateRootWorkers
                });

            return new FollowerExecutorStack(stack.Importer, (IncrementalStateRootCalculator)stack.Calculator);
        }
    }
}
