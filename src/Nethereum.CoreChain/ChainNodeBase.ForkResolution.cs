using System;
using System.Collections.Concurrent;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public abstract partial class ChainNodeBase
    {
        private readonly HardforkConfig _staticHardforkConfig;
        private readonly IChainActivations _activations;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly ConcurrentDictionary<HardforkName, HardforkConfig> _hardforkConfigByFork
            = new ConcurrentDictionary<HardforkName, HardforkConfig>();
        private readonly ConcurrentDictionary<HardforkName, TransactionExecutor> _executorByFork
            = new ConcurrentDictionary<HardforkName, TransactionExecutor>();

        private bool HasInjectedActivations => _activations != null && _hardforkConfigFactory != null;

        private bool HasScheduledForks => Config.ForkSchedule != null
            && Config.ForkSchedule.Schedule != null
            && Config.ForkSchedule.Schedule.Count > 0;

        private bool FollowsAForkSchedule => HasInjectedActivations || Config.Activations != null || HasScheduledForks;

        protected HardforkName ResolveHardforkName(long blockNumber, ulong timestamp)
            => HasInjectedActivations
                ? _activations.ResolveAt(blockNumber, timestamp)
                : Config.ResolveHardforkAt(blockNumber, timestamp);

        protected HardforkConfig ResolveHardforkConfig(long blockNumber, ulong timestamp)
        {
            if (HasInjectedActivations)
                return _hardforkConfigByFork.GetOrAdd(_activations.ResolveAt(blockNumber, timestamp), _hardforkConfigFactory);

            if (!FollowsAForkSchedule) return _staticHardforkConfig;

            return Config.GetHardforkConfigAt(blockNumber, timestamp);
        }

        protected TransactionExecutor ResolveExecutor(long blockNumber, ulong timestamp)
        {
            if (!FollowsAForkSchedule) return _executor;

            return _executorByFork.GetOrAdd(
                ResolveHardforkName(blockNumber, timestamp),
                _ => new TransactionExecutor(ResolveHardforkConfig(blockNumber, timestamp)));
        }

        protected TransactionExecutor ExecutorAt(BlockContext blockContext)
        {
            if (blockContext == null) return _executor;
            return ResolveExecutor((long)blockContext.BlockNumber, (ulong)blockContext.Timestamp);
        }

        protected TransactionExecutor ExecutorAt(long blockNumber, ulong timestamp)
            => ResolveExecutor(blockNumber, timestamp);

        private ChainConfig ForkStampedConfigFor(BlockHeader header) => new ChainConfig
        {
            ChainId = Config.ChainId,
            BaseFee = Config.BaseFee,
            Registry = Config.Registry,
            Hardfork = ResolveHardforkName((long)header.BlockNumber, (ulong)header.Timestamp)
                .ToString()
                .ToLowerInvariant()
        };
    }
}
