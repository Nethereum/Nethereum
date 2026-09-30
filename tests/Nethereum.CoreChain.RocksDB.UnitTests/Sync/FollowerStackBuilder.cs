using System;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.EVM;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public static class FollowerStackBuilder
    {
        public static IBlockExecutor Build(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            Func<HardforkName, ChainConfig> chainConfigFactory)
            => FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                EthereumProofOfWorkRewardPolicy.Instance);

        public static IBlockExecutor BuildPathKeyed(
            IChainStoreBundle bundle,
            IChainActivations activations,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            Func<HardforkName, ChainConfig> chainConfigFactory,
            IFlushCadence flushCadence = null)
            => FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                bundle, activations, chainConfigFactory, hardforkConfigFactory,
                EthereumProofOfWorkRewardPolicy.Instance, flushCadence: flushCadence);
    }
}
