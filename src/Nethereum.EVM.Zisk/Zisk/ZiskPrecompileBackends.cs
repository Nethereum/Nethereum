using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Zisk.Backends;

namespace Nethereum.EVM.Zisk
{
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
