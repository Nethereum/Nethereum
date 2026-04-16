using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles
{
    /// <summary>
    /// Bundle of precompile backend implementations. Ethereum mainnet's fork definition,
    /// opcode table, gas rules, and intrinsic rules are identical across every consumer —
    /// what varies is the concrete crypto backend (managed .NET, witness-backed for zkVMs,
    /// formally-verified sha256, etc.). <see cref="MainnetHardforkRegistry.Build"/>
    /// takes an instance of this bundle to compose a registry against any backend choice.
    ///
    /// <see cref="P256Verify"/> is only required for Osaka (EIP-7951). Pre-Osaka registries
    /// are built without it; the null check triggers only if Osaka is included.
    /// </summary>
    public class PrecompileBackends
    {
        public IEcRecoverBackend EcRecover { get; }
        public ISha256Backend Sha256 { get; }
        public IRipemd160Backend Ripemd160 { get; }
        public IModExpBackend ModExp { get; }
        public IBn128Backend Bn128 { get; }
        public IBlake2fBackend Blake2f { get; }
        public IP256VerifyBackend P256Verify { get; }

        public PrecompileBackends(
            IEcRecoverBackend ecRecover,
            ISha256Backend sha256,
            IRipemd160Backend ripemd160,
            IModExpBackend modExp,
            IBn128Backend bn128,
            IBlake2fBackend blake2f,
            IP256VerifyBackend p256Verify = null)
        {
            EcRecover = ecRecover ?? throw new ArgumentNullException(nameof(ecRecover));
            Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
            Ripemd160 = ripemd160 ?? throw new ArgumentNullException(nameof(ripemd160));
            ModExp = modExp ?? throw new ArgumentNullException(nameof(modExp));
            Bn128 = bn128 ?? throw new ArgumentNullException(nameof(bn128));
            Blake2f = blake2f ?? throw new ArgumentNullException(nameof(blake2f));
            P256Verify = p256Verify;
        }
    }
}
