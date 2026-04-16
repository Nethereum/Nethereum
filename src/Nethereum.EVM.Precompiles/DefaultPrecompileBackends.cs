using Nethereum.EVM.Execution.Precompiles;

namespace Nethereum.EVM.Precompiles
{
    /// <summary>
    /// Nethereum's default precompile backend bundle. Ships the managed .NET
    /// implementations of ECRECOVER, SHA256, RIPEMD160, MODEXP, BN128 add/mul/pairing,
    /// BLAKE2F, and P256VERIFY. Feed <see cref="Instance"/> into
    /// <see cref="Nethereum.EVM.MainnetHardforkRegistry.Build"/> to assemble an Ethereum
    /// mainnet registry bound to these defaults. Other backend providers (Zisk witness,
    /// risc0, SP1, custom crypto) ship their own equivalent bundle.
    /// </summary>
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
