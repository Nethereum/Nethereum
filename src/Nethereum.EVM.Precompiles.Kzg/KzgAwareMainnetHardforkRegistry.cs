using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM.Precompiles.Kzg
{
    public static class KzgAwareMainnetHardforkRegistry
    {
        public static readonly HardforkRegistry Instance = Build();

        private static HardforkRegistry Build()
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();
            return Build(DefaultMainnetHardforkRegistry.Instance, kzg);
        }

        public static HardforkRegistry Build(HardforkRegistry baseRegistry, IKzgOperations kzg)
        {
            if (baseRegistry == null) throw new System.ArgumentNullException(nameof(baseRegistry));
            if (kzg == null) throw new System.ArgumentNullException(nameof(kzg));

            var result = new HardforkRegistry();
            foreach (var name in baseRegistry.RegisteredNames)
            {
                var config = baseRegistry.Get(name);
                if (name >= HardforkName.Cancun && config.Precompiles != null)
                {
                    var upgraded = config.Clone();
                    upgraded.Precompiles = config.Precompiles.WithKzgBackend(kzg);
                    result.Register(name, upgraded);
                }
                else
                {
                    result.Register(name, config);
                }
            }
            return result;
        }
    }
}
