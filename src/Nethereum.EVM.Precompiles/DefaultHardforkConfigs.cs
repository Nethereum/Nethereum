using Nethereum.EVM;

namespace Nethereum.EVM.Precompiles
{
    /// <summary>
    /// Pre-wired precompile-included <see cref="HardforkConfig"/> instances per fork.
    /// Consumers should use <see cref="MainnetHardforkRegistry"/> for fork lookup;
    /// these statics exist so the registry has something to register and so
    /// single-fork callers (bundlers, wallets) can pin one fork.
    /// </summary>
    public static class DefaultHardforkConfigs
    {
        // Frontier/Homestead: only precompiles 1-4 (ecrecover, sha256, ripemd160, identity)
        private static readonly HardforkConfig _frontier =
            HardforkConfig.Frontier.WithPrecompiles(DefaultPrecompileRegistries.FrontierBase());
        private static readonly HardforkConfig _homestead =
            HardforkConfig.Homestead.WithPrecompiles(DefaultPrecompileRegistries.FrontierBase());
        private static readonly HardforkConfig _tangerineWhistle =
            HardforkConfig.TangerineWhistle.WithPrecompiles(DefaultPrecompileRegistries.FrontierBase());
        private static readonly HardforkConfig _spuriousDragon =
            HardforkConfig.SpuriousDragon.WithPrecompiles(DefaultPrecompileRegistries.FrontierBase());
        private static readonly HardforkConfig _byzantium =
            HardforkConfig.Byzantium.WithPrecompiles(DefaultPrecompileRegistries.ByzantiumBase());
        private static readonly HardforkConfig _constantinople =
            HardforkConfig.Constantinople.WithPrecompiles(DefaultPrecompileRegistries.ByzantiumBase());
        private static readonly HardforkConfig _petersburg =
            HardforkConfig.Petersburg.WithPrecompiles(DefaultPrecompileRegistries.ByzantiumBase());
        private static readonly HardforkConfig _istanbul =
            HardforkConfig.Istanbul.WithPrecompiles(DefaultPrecompileRegistries.IstanbulBase());
        private static readonly HardforkConfig _berlin =
            HardforkConfig.Berlin.WithPrecompiles(DefaultPrecompileRegistries.BerlinBase());
        private static readonly HardforkConfig _london =
            HardforkConfig.London.WithPrecompiles(DefaultPrecompileRegistries.BerlinBase());
        private static readonly HardforkConfig _paris =
            HardforkConfig.Paris.WithPrecompiles(DefaultPrecompileRegistries.BerlinBase());
        private static readonly HardforkConfig _shanghai =
            HardforkConfig.Shanghai.WithPrecompiles(DefaultPrecompileRegistries.BerlinBase());

        private static readonly HardforkConfig _cancun =
            HardforkConfig.Cancun.WithPrecompiles(DefaultPrecompileRegistries.CancunBase());
        private static readonly HardforkConfig _prague =
            HardforkConfig.Prague.WithPrecompiles(DefaultPrecompileRegistries.PragueBase());
        private static readonly HardforkConfig _osaka =
            HardforkConfig.Osaka.WithPrecompiles(DefaultPrecompileRegistries.OsakaBase());

        public static HardforkConfig Frontier => _frontier;
        public static HardforkConfig Homestead => _homestead;
        public static HardforkConfig TangerineWhistle => _tangerineWhistle;
        public static HardforkConfig SpuriousDragon => _spuriousDragon;
        public static HardforkConfig Byzantium => _byzantium;
        public static HardforkConfig Constantinople => _constantinople;
        public static HardforkConfig Petersburg => _petersburg;
        public static HardforkConfig Istanbul => _istanbul;
        public static HardforkConfig Berlin => _berlin;
        public static HardforkConfig London => _london;
        public static HardforkConfig Paris => _paris;
        public static HardforkConfig Shanghai => _shanghai;
        public static HardforkConfig Cancun => _cancun;
        public static HardforkConfig Prague => _prague;
        public static HardforkConfig Osaka => _osaka;
        public static HardforkConfig Default => _osaka;
    }
}
