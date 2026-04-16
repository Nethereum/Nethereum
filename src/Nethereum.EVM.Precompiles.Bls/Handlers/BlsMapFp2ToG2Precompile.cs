using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    /// <summary>
    /// Precompile 0x11 — BLS12-381 MAP_FP2_TO_G2 (EIP-2537). Added in Prague.
    /// Maps a 128-byte Fp2 field element to a point on G2.
    ///
    /// Input: 128 bytes (one Fp2 element — two 64-byte padded Fp coords).
    /// Output: 256 bytes (one G2 point).
    ///
    /// Gas cost (23800 per EIP-2537) lives on the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    /// </summary>
    public sealed class BlsMapFp2ToG2Precompile : PrecompileHandlerBase
    {
        private const int Fp2Size = 128;
        private const int G2PointSize = 256;

        private readonly IBls12381Operations _ops;

        public BlsMapFp2ToG2Precompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x11;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, Fp2Size, "BLS12-381 MAP_FP2_TO_G2");

            var result = _ops.MapFp2ToG2(input);
            return result.Length == G2PointSize ? result : result.PadBytes(G2PointSize);
        }
    }
}
