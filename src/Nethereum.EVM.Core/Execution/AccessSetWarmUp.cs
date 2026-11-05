using Nethereum.EVM.BlockchainState;
using Nethereum.Util;

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
            WarmOriginPrecompilesAndCoinbase(
                executionState,
                config,
                EvmAddress.FromHex(origin),
                string.IsNullOrEmpty(coinbase) ? (EvmAddress?)null : EvmAddress.FromHex(coinbase));
        }

        public static void WarmOriginPrecompilesAndCoinbase(
            ExecutionStateService executionState,
            HardforkConfig config,
            EvmAddress origin,
            EvmAddress? coinbase)
        {
            executionState.MarkAddressAsWarm(origin);
            executionState.MarkPrecompilesAsWarm(config.Precompiles);

            if (config.WarmCoinbase && coinbase.HasValue)
                executionState.MarkAddressAsWarm(coinbase.Value);
        }
    }
}
