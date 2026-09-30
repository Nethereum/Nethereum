using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Precompiles.Kzg.Handlers;

namespace Nethereum.EVM.Precompiles.Kzg
{
    public static class PrecompileRegistryKzgExtensions
    {
        /// <summary>
        /// Returns a new immutable registry with the KZG point evaluation
        /// handler installed at address 0x0a. Late-wins: replaces any
        /// existing handler at that address.
        /// </summary>
        public static PrecompileRegistry WithKzgBackend(
            this PrecompileRegistry registry,
            IKzgOperations kzg)
        {
            if (registry == null) throw new System.ArgumentNullException(nameof(registry));
            if (kzg == null) throw new System.ArgumentNullException(nameof(kzg));

            return registry.WithHandlers(new KzgPointEvaluationPrecompile(kzg));
        }

        /// <summary>
        /// Convenience overload: uses the embedded CKZG trusted setup.
        /// Equivalent to <c>.WithKzgBackend(new CkzgOperations())</c> after
        /// calling <c>CkzgOperations.InitializeFromEmbeddedSetup()</c>.
        /// </summary>
        public static PrecompileRegistry WithKzgBackend(this PrecompileRegistry registry)
        {
            CkzgOperations.InitializeFromEmbeddedSetup();
            return registry.WithKzgBackend(new CkzgOperations());
        }
    }
}
