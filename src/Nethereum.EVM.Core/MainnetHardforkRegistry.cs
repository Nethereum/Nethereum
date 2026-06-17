using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Hardforks;

namespace Nethereum.EVM
{
    /// <summary>
    /// Builds a <see cref="HardforkRegistry"/> populated with every Ethereum
    /// mainnet fork (Frontier → Osaka) by iterating
    /// <see cref="HardforkSpecRegistry.All"/> and resolving each spec to a
    /// runtime <see cref="HardforkConfig"/> via
    /// <see cref="HardforkConfigFromSpec.BuildWithPrecompiles"/>. The spec is
    /// the single source of truth for opcode tables, gas rules, intrinsic
    /// rules, call-frame rules and the active precompile set; this factory
    /// only injects the concrete crypto backends through
    /// <see cref="MainnetPrecompileExecutorFactory"/>.
    ///
    /// <para>For the Nethereum default backends use
    /// <c>DefaultMainnetHardforkRegistry.Instance</c> in
    /// <c>Nethereum.EVM.Precompiles</c> — a pre-built singleton composed
    /// from <c>DefaultPrecompileBackends.Instance</c>.</para>
    /// </summary>
    public static class MainnetHardforkRegistry
    {
        public static HardforkRegistry Build(PrecompileBackends backends)
        {
            if (backends is null) throw new System.ArgumentNullException(nameof(backends));

            var factory = new MainnetPrecompileExecutorFactory(backends);
            var r = new HardforkRegistry();

            foreach (var spec in HardforkSpecRegistry.All)
                r.Register(spec.Name, HardforkConfigFromSpec.BuildWithPrecompiles(spec, factory));

            // Difficulty-bomb / consensus-only aliases that are EVM-identical
            // to their parent fork.
            r.Register(HardforkName.FrontierThawing, r.Get(HardforkName.Frontier));
            r.Register(HardforkName.DaoFork, r.Get(HardforkName.Homestead));
            r.Register(HardforkName.MuirGlacier, r.Get(HardforkName.Istanbul));
            r.Register(HardforkName.ArrowGlacier, r.Get(HardforkName.London));
            r.Register(HardforkName.GrayGlacier, r.Get(HardforkName.London));

            return r;
        }
    }
}
