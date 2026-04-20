using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Zisk.Backends;

namespace Nethereum.EVM.Zisk
{
    /// <summary>
    /// Zisk / zkVM witness-backed crypto backend bundle for the Frontier-to-Istanbul
    /// precompiles (0x01..0x09). Wire this into <see cref="MainnetHardforkRegistry.Build"/>
    /// to obtain a mainnet registry whose core crypto dispatches to ZiskCrypto P/Invokes
    /// (CSR precompile instructions under the hood).
    ///
    /// BLS12-381 (0x0b..0x11, Prague+) and KZG point evaluation (0x0a, Cancun+) are
    /// layered on top of the registry at runtime by
    /// <c>ZiskBinaryWitness.LayerKzgAndBlsBackends</c>, using
    /// <see cref="ZiskBls12381Operations"/> and <see cref="ZiskKzgOperations"/>.
    ///
    /// P256Verify (0x100, Osaka) is null — Osaka is still registered with Prague-level
    /// precompiles so the fork's EVM rules (opcodes, gas) are available; only the P256
    /// precompile at 0x100 is absent until a <c>ZiskP256VerifyBackend</c> exists.
    /// </summary>
    public static class ZiskPrecompileBackends
    {
        public static readonly PrecompileBackends Instance = new PrecompileBackends(
            ZiskEcRecoverBackend.Instance,
            ZiskSha256Backend.Instance,
            ZiskRipemd160Backend.Instance,
            ZiskModExpBackend.Instance,
            ZiskBn128Backend.Instance,
            ZiskBlake2fBackend.Instance);
    }
}
