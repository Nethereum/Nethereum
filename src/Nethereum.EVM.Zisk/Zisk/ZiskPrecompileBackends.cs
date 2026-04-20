using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Zisk.Backends;

namespace Nethereum.EVM.Zisk
{
    /// <summary>
    /// Zisk / zkVM witness-backed crypto backend bundle. Wire this into
    /// <see cref="MainnetHardforkRegistry.Build"/> to obtain a mainnet registry whose
    /// core crypto dispatches to ZiskCrypto P/Invokes (CSR precompile instructions
    /// under the hood).
    ///
    /// Covers:
    /// - Frontier-to-Istanbul precompiles 0x01..0x09 directly.
    /// - P256VERIFY (0x100, Osaka) via <see cref="ZiskP256VerifyBackend"/>, wrapping
    ///   <c>secp256r1_ecdsa_verify_c</c> (CSR 0x817 / 0x818).
    /// - BLS12-381 (0x0b..0x11, Prague+) and KZG point evaluation (0x0a, Cancun+)
    ///   are layered on the registry at runtime by
    ///   <c>ZiskBinaryWitness.LayerKzgAndBlsBackends</c>, using
    ///   <see cref="ZiskBls12381Operations"/> and <see cref="ZiskKzgOperations"/>.
    /// </summary>
    public static class ZiskPrecompileBackends
    {
        public static readonly PrecompileBackends Instance = new PrecompileBackends(
            ZiskEcRecoverBackend.Instance,
            ZiskSha256Backend.Instance,
            ZiskRipemd160Backend.Instance,
            ZiskModExpBackend.Instance,
            ZiskBn128Backend.Instance,
            ZiskBlake2fBackend.Instance,
            ZiskP256VerifyBackend.Instance);
    }
}
