using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    /// <summary>
    /// Precompile 0x0e — BLS12-381 G2MSM (EIP-2537). Added in Prague.
    /// Computes a multi-scalar multiplication of G2 points.
    ///
    /// Input: 288*k bytes (k pairs of 256-byte G2 point + 32-byte scalar).
    /// Output: 256 bytes (one G2 point).
    ///
    /// Gas cost (discounted per pair count) lives on the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    /// </summary>
    public sealed class BlsG2MsmPrecompile : PrecompileHandlerBase
    {
        private const int G2PointSize = 256;
        private const int ScalarSize = 32;
        private const int ElementSize = G2PointSize + ScalarSize;

        private readonly IBls12381Operations _ops;

        public BlsG2MsmPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x0e;

        public override byte[] Execute(byte[] input)
        {
            RequireInputMultiple(input, ElementSize, "BLS12-381 G2MSM");

            int k = input.Length / ElementSize;
            var points = new byte[k][];
            var scalars = new byte[k][];

            for (int i = 0; i < k; i++)
            {
                int pointStart = i * ElementSize;
                int scalarStart = pointStart + G2PointSize;
                points[i] = input.Slice(pointStart, scalarStart);
                scalars[i] = input.Slice(scalarStart, scalarStart + ScalarSize);
            }

            var result = _ops.G2Msm(points, scalars);
            return result.Length == G2PointSize ? result : result.PadBytes(G2PointSize);
        }
    }
}
