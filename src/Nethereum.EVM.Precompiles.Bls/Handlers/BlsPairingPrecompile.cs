using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    /// <summary>
    /// Precompile 0x0f — BLS12-381 PAIRING (EIP-2537). Added in Prague.
    /// Checks whether the product of k pairings e(G1_i, G2_i) equals 1.
    ///
    /// Input: 384*k bytes (k pairs of 128-byte G1 point + 256-byte G2 point).
    /// Output: 32 bytes. <c>0x00..01</c> when the pairing check passes,
    /// <c>0x00..00</c> otherwise. Empty input (k=0) is an error per the
    /// execution-specs Python reference and go-ethereum
    /// (<c>errBLS12381InvalidInputLength</c>); it is NOT interpreted as a
    /// successful "pairing of the empty set".
    ///
    /// Gas cost (base + per-pair) lives on the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    /// </summary>
    public sealed class BlsPairingPrecompile : PrecompileHandlerBase
    {
        private const int G1PointSize = 128;
        private const int G2PointSize = 256;
        private const int PairSize = G1PointSize + G2PointSize;

        private readonly IBls12381Operations _ops;

        public BlsPairingPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x0f;

        public override byte[] Execute(byte[] input)
        {
            var data = OrEmpty(input);
            if (data.Length == 0 || (data.Length % PairSize) != 0)
                throw new ArgumentException(
                    $"BLS12-381 PAIRING: expected non-empty multiple of {PairSize} bytes, got {data.Length}");

            int k = data.Length / PairSize;
            var g1Points = new byte[k][];
            var g2Points = new byte[k][];

            for (int i = 0; i < k; i++)
            {
                int pairStart = i * PairSize;
                int g2Start = pairStart + G1PointSize;
                g1Points[i] = data.Slice(pairStart, g2Start);
                g2Points[i] = data.Slice(g2Start, g2Start + G2PointSize);
            }

            var passed = _ops.Pairing(g1Points, g2Points);
            var output = new byte[32];
            if (passed) output[31] = 1;
            return output;
        }
    }
}
