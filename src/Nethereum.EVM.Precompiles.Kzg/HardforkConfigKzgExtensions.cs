using Nethereum.EVM.Precompiles.Kzg;

namespace Nethereum.EVM
{
    public static class HardforkConfigKzgExtensions
    {
        public static HardforkConfig WithKzgBackend(this HardforkConfig config)
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            return config.WithKzgBackend(new CkzgOperations());
        }

        public static HardforkConfig WithKzgBackend(this HardforkConfig config, IKzgOperations kzgOperations)
        {
            if (config.Precompiles == null)
                throw new System.InvalidOperationException(
                    "WithKzgBackend requires a base precompile registry on the config. " +
                    "Obtain a config via MainnetHardforkRegistry.Build(backends).Get(HardforkName.Cancun) " +
                    "before calling WithKzgBackend.");

            var registry = config.Precompiles.WithKzgBackend(kzgOperations);
            return config.WithPrecompiles(registry);
        }
    }
}
