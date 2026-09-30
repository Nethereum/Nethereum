using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Hardforks;

namespace Nethereum.EVM
{
    public static class MainnetHardforkRegistry
    {
        public static HardforkRegistry Build(PrecompileBackends backends)
        {
            if (backends is null) throw new System.ArgumentNullException(nameof(backends));

            var factory = new MainnetPrecompileExecutorFactory(backends);
            var r = new HardforkRegistry();

            foreach (var spec in HardforkSpecRegistry.All)
                r.Register(spec.Name, HardforkConfigFromSpec.BuildWithPrecompiles(spec, factory));

            r.Register(HardforkName.FrontierThawing, r.Get(HardforkName.Frontier));
            r.Register(HardforkName.DaoFork, r.Get(HardforkName.Homestead));
            r.Register(HardforkName.MuirGlacier, r.Get(HardforkName.Istanbul));
            r.Register(HardforkName.ArrowGlacier, r.Get(HardforkName.London));
            r.Register(HardforkName.GrayGlacier, r.Get(HardforkName.London));

            return r;
        }
    }
}
