using System;
using System.Linq;
using Nethereum.EVM;

namespace Nethereum.EVM.Precompiles
{
    public static class DefaultChainForkResolver
    {
        public static readonly HardforkName NewestImplementedFork =
            Enum.GetValues(typeof(HardforkName)).Cast<HardforkName>()
                .Where(f => f != HardforkName.Unspecified)
                .Max();

        public static ChainForkResolver Default => AssumingUndescribedChainsRun(NewestImplementedFork);

        public static ChainForkResolver AssumingUndescribedChainsRun(HardforkName fork)
        {
            var chains = new ChainActivationsRegistry
            {
                DefaultForUnregisteredChains = ScheduledChainActivations.RunningOnly(fork)
            };

            return new ChainForkResolver(chains, DefaultMainnetHardforkRegistry.Instance);
        }
    }
}
