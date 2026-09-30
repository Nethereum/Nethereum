using Nethereum.EVM;

namespace Nethereum.EVM.Precompiles
{
    public static class DefaultMainnetHardforkRegistry
    {
        public static readonly HardforkRegistry Instance =
            MainnetHardforkRegistry.Build(DefaultPrecompileBackends.Instance);
    }
}
