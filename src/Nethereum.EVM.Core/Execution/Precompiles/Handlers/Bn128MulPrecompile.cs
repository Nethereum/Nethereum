using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x07 — BN128_MUL (a.k.a. ECMUL). Scalar multiplication on
    /// the alt_bn128 curve. Added in Byzantium (EIP-196); repriced cheaper
    /// in Istanbul (EIP-1108). Gas cost is owned by the fork's
    /// <see cref="PrecompileGasCalculators"/>; the underlying curve
    /// operation is provided by an <see cref="IBn128Backend"/>.
    /// </summary>
    public sealed class Bn128MulPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;

        public Bn128MulPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 7;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Mul(input);
        }
    }
}
