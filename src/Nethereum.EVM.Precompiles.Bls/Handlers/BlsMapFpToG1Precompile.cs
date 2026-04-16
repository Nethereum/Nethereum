using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    /// <summary>
    /// Precompile 0x10 — BLS12-381 MAP_FP_TO_G1 (EIP-2537). Added in Prague.
    /// Maps a 64-byte Fp field element to a point on G1.
    ///
    /// Input: 64 bytes (one Fp element, padded to 64).
    /// Output: 128 bytes (one G1 point).
    ///
    /// Gas cost (5500 per EIP-2537) lives on the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    /// </summary>
    public sealed class BlsMapFpToG1Precompile : PrecompileHandlerBase
    {
        private const int FpSize = 64;
        private const int G1PointSize = 128;

        private readonly IBls12381Operations _ops;

        public BlsMapFpToG1Precompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x10;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, FpSize, "BLS12-381 MAP_FP_TO_G1");

            var result = _ops.MapFpToG1(input);
            return result.Length == G1PointSize ? result : result.PadBytes(G1PointSize);
        }
    }
}
