using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Zisk.Backends;

namespace Nethereum.EVM.Zisk
{
    /// <summary>
    /// Zisk / zkVM witness-backed crypto backend bundle. Wire this into
    /// <see cref="MainnetHardforkRegistry.Build"/> to obtain a mainnet
    /// registry that runs the self-contained precompiles 0x01..0x09 via
    /// ZiskCrypto P/Invoke. P256Verify is null — Osaka is not registered
    /// until a ZiskP256VerifyBackend exists. BLS12-381 and KZG still flow
    /// through the legacy <c>ZiskPrecompileProvider</c> fallback.
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
