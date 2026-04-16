using System;
using Nethereum.EVM.Execution.Precompiles.CryptoBackends;

namespace Nethereum.EVM.Execution.Precompiles.Handlers
{
    /// <summary>
    /// Precompile 0x08 — BN128_PAIRING (a.k.a. ECPAIRING). Optimal ate
    /// pairing check on the alt_bn128 curve. Added in Byzantium (EIP-197);
    /// repriced cheaper in Istanbul (EIP-1108). Gas cost (base + per-pair)
    /// is owned by the fork's <see cref="PrecompileGasCalculators"/>; the
    /// underlying curve operation is provided by an
    /// <see cref="IBn128Backend"/>.
    /// </summary>
    public sealed class Bn128PairingPrecompile : PrecompileHandlerBase
    {
        private readonly IBn128Backend _backend;

        public Bn128PairingPrecompile(IBn128Backend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public override int AddressNumeric => 8;

        public override byte[] Execute(byte[] input)
        {
            return _backend.Pairing(input);
        }
    }
}
