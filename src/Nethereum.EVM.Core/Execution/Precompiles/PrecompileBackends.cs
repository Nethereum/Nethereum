using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles
{
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
