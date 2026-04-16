using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x06 — BN128_ADD (a.k.a. ECADD). Adds two points on the
    /// alt_bn128 curve. Added in Byzantium (EIP-196); repriced cheaper in
    /// Istanbul (EIP-1108). Gas cost is owned by the fork's
    /// <see cref="PrecompileGasCalculators"/>; the underlying curve
    /// operation is provided by an <see cref="IBn128Backend"/> so the
    /// production build uses the managed <c>BN128Curve</c> and Zisk uses
    /// a witness-backed variant.
    /// </summary>
    public sealed class Bn128AddPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;

        public Bn128AddPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 6;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Add(input);
        }
    }
}
