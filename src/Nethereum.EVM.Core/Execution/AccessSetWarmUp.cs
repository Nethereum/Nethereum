using Nethereum.EVM.BlockchainState;

namespace Nethereum.EVM.Execution
{
    public static class AccessSetWarmUp
    {
        public static void WarmOriginPrecompilesAndCoinbase(
            ExecutionStateService executionState,
            HardforkConfig config,
            string origin,
            string coinbase)
        {
            executionState.MarkAddressAsWarm(origin);
            executionState.MarkPrecompilesAsWarm(config.Precompiles);

            if (config.WarmCoinbase && !string.IsNullOrEmpty(coinbase))
                executionState.MarkAddressAsWarm(coinbase);
        }
    }
}
