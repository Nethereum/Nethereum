using Nethereum.EVM.Precompiles.Bls;
using Nethereum.Signer.Bls;

namespace Nethereum.EVM
{
    public static class HardforkConfigBlsExtensions
    {
        public static HardforkConfig WithBlsBackend(this HardforkConfig config, IBls12381Operations blsOperations)
        {
            if (config.Precompiles == null)
                throw new System.InvalidOperationException(
                    "WithBlsBackend requires a base precompile registry on the config. " +
                    "Obtain a config via MainnetHardforkRegistry.Build(backends).Get(HardforkName.Prague) " +
                    "before calling WithBlsBackend.");

            var registry = config.Precompiles.WithBlsBackend(blsOperations);
            return config.WithPrecompiles(registry);
        }
    }
}
