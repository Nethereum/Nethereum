using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Precompiles.Bls.Handlers;
using Nethereum.Signer.Bls;

namespace Nethereum.EVM.Precompiles.Bls
{
    /// <summary>
    /// Extension methods that layer the seven EIP-2537 BLS12-381 precompile
    /// handlers (0x0b..0x11) onto an existing <see cref="PrecompileRegistry"/>
    /// using a caller-supplied <see cref="IBls12381Operations"/> backend.
    /// The backend is the only injection point: production wires in
    /// <c>Nethereum.Signer.Bls.Herumi.Bls12381Operations</c>, the Zisk sync
    /// path wires in a witness-backed variant. No gas calculation lives on
    /// the extension — gas is owned by the registry's
    /// <see cref="PrecompileGasCalculators"/> (which Prague and Osaka already
    /// have in their base registries).
    /// </summary>
    public static class PrecompileRegistryBlsExtensions
    {
        /// <summary>
        /// Returns a new immutable registry with the seven BLS12-381
        /// handlers added to <paramref name="registry"/>. Late-wins
        /// semantic: if the registry already has a handler at any of
        /// 0x0b..0x11, it is replaced.
        /// </summary>
        public static PrecompileRegistry WithBlsBackend(
            this PrecompileRegistry registry,
            IBls12381Operations bls)
        {
            if (registry == null) throw new System.ArgumentNullException(nameof(registry));
            if (bls == null) throw new System.ArgumentNullException(nameof(bls));

            return registry.WithHandlers(
                new BlsG1AddPrecompile(bls),
                new BlsG1MsmPrecompile(bls),
                new BlsG2AddPrecompile(bls),
                new BlsG2MsmPrecompile(bls),
                new BlsPairingPrecompile(bls),
                new BlsMapFpToG1Precompile(bls),
                new BlsMapFp2ToG2Precompile(bls));
        }
    }
}
