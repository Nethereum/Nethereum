using Nethereum.EVM;

namespace Nethereum.EVM.Precompiles
{
    /// <summary>
    /// Pre-built Ethereum mainnet registry using Nethereum's default precompile
    /// backends (<see cref="DefaultPrecompileBackends.Instance"/>). This is the
    /// convenience singleton most Nethereum-hosted consumers want; Core's
    /// <see cref="MainnetHardforkRegistry.Build"/> stays available for callers
    /// that supply a different backend bundle (Zisk witness, custom crypto, …).
    /// </summary>
    public static class DefaultMainnetHardforkRegistry
    {
        public static readonly HardforkRegistry Instance =
            MainnetHardforkRegistry.Build(DefaultPrecompileBackends.Instance);
    }
}
