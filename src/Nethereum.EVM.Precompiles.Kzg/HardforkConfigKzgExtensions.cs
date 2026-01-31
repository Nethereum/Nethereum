namespace Nethereum.EVM
{
    public static class HardforkConfigKzgExtensions
    {
        public static HardforkConfig WithKzgPrecompiles(this HardforkConfig config)
        {
            Precompiles.Kzg.CkzgOperations.InitializeFromEmbeddedSetup();
            var kzgProvider = new Precompiles.Kzg.KzgPrecompileProvider(new Precompiles.Kzg.CkzgOperations());
            return config.WithPrecompileProviders(kzgProvider);
        }

        public static HardforkConfig WithKzgPrecompiles(this HardforkConfig config, Precompiles.Kzg.IKzgOperations kzgOperations)
        {
            var kzgProvider = new Precompiles.Kzg.KzgPrecompileProvider(kzgOperations);
            return config.WithPrecompileProviders(kzgProvider);
        }
    }
}
