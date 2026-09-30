using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM.Precompiles
{
    public static class DefaultPrecompileBackends
    {
        public static readonly PrecompileBackends Instance = new PrecompileBackends(
            ecRecover: Backends.DefaultEcRecoverBackend.Instance,
            sha256: Backends.DefaultSha256Backend.Instance,
            ripemd160: Backends.DefaultRipemd160Backend.Instance,
            modExp: Backends.DefaultModExpBackend.Instance,
            bn128: Backends.DefaultBn128Backend.Instance,
            blake2f: Backends.DefaultBlake2fBackend.Instance,
            p256Verify: Backends.DefaultP256VerifyBackend.Instance);
    }
}
