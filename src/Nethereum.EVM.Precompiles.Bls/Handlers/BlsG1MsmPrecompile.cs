using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsG1MsmPrecompile : PrecompileHandlerBase
    {
        private const int G1PointSize = 128;
        private const int ScalarSize = 32;
        private const int ElementSize = G1PointSize + ScalarSize;

        private readonly IBls12381Operations _ops;

        public BlsG1MsmPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x0c;

        public override byte[] Execute(byte[] input)
        {
            RequireInputMultiple(input, ElementSize, "BLS12-381 G1MSM");

            int k = input.Length / ElementSize;
            var points = new byte[k][];
            var scalars = new byte[k][];

            for (int i = 0; i < k; i++)
            {
                int pointStart = i * ElementSize;
                int scalarStart = pointStart + G1PointSize;
                points[i] = input.Slice(pointStart, scalarStart);
                scalars[i] = input.Slice(scalarStart, scalarStart + ScalarSize);
            }

            var result = _ops.G1Msm(points, scalars);
            return result.Length == G1PointSize ? result : result.PadBytes(G1PointSize);
        }
    }
}
