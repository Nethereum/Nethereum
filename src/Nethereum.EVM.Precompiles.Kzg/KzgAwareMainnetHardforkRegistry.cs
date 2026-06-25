using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM.Precompiles.Kzg
{
    /// <summary>
    /// Returns a Cancun+ KZG-enabled <see cref="HardforkRegistry"/>:
    /// every fork from Cancun onwards has its <c>PrecompileRegistry</c>
    /// rebuilt via <see cref="PrecompileRegistryKzgExtensions.WithKzgBackend"/>,
    /// installing the real EIP-4844 point evaluation precompile at
    /// address <c>0x0a</c> in place of the
    /// <see cref="Execution.Precompiles.Handlers.PlaceholderPrecompile"/>
    /// that the placeholder factory installs by default.
    ///
    /// <para>Without this layer, every EEST
    /// <c>cancun/eip4844_blobs/test_point_evaluation_precompile</c>
    /// fixture fails on a state-root mismatch — the placeholder throws
    /// instead of running KZG verification, so the precompile CALL
    /// reverts and the contract takes the failure branch. Layered tests
    /// (mainnet replay, blob-tx integration) see the same gap.</para>
    /// </summary>
    public static class KzgAwareMainnetHardforkRegistry
    {
        /// <summary>
        /// Lazy singleton: same backends as
        /// <see cref="DefaultMainnetHardforkRegistry.Instance"/> with the
        /// embedded CKZG trusted setup loaded once at first access.
        /// </summary>
        public static readonly HardforkRegistry Instance = Build();

        private static HardforkRegistry Build()
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            var kzg = new CkzgOperations();
            return Build(DefaultMainnetHardforkRegistry.Instance, kzg);
        }

        /// <summary>
        /// Returns a new <see cref="HardforkRegistry"/> where every fork
        /// from <see cref="HardforkName.Cancun"/> onwards has its
        /// precompile registry layered with the supplied KZG backend.
        /// Pre-Cancun forks (where 0x0a is not a precompile) are passed
        /// through unchanged.
        /// </summary>
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
