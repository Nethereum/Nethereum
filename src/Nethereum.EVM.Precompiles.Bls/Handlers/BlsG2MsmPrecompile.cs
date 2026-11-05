using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsG2MsmPrecompile : PrecompileHandlerBase
    {
        private const int PointSize = 256;
        private const int ScalarSize = 32;
        private const int ElementSize = PointSize + ScalarSize;

        private readonly IBls12381Operations _ops;
        private readonly IBls12381OutcomeOperations _outcomes;

        public BlsG2MsmPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
            _outcomes = ops as IBls12381OutcomeOperations;
        }

        public override int AddressNumeric => 0x0e;

        public override byte[] Execute(byte[] input)
        {
            RequireInputMultiple(input, ElementSize, "BLS12-381 G2MSM");
            return TryExecute(input, out var output)
                ? output
                : throw new ArgumentException("BLS12-381 G2MSM: invalid point or scalar encoding");
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            if (!HasInputMultiple(input, ElementSize))
            {
                output = null;
                return false;
            }

            int k = input.Length / ElementSize;
            var points = new byte[k][];
            var scalars = new byte[k][];
            for (int i = 0; i < k; i++)
            {
                int pointStart = i * ElementSize;
                int scalarStart = pointStart + PointSize;
                points[i] = input.Slice(pointStart, scalarStart);
                scalars[i] = input.Slice(scalarStart, scalarStart + ScalarSize);
            }

            byte[] result;
            if (_outcomes != null)
            {
                if (!_outcomes.TryG2Msm(points, scalars, out result))
                {
                    output = null;
                    return false;
                }
            }
            else if (!TryThroughThrowingOperation(() => _ops.G2Msm(points, scalars), out result))
            {
                output = null;
                return false;
            }

            output = result.Length == PointSize ? result : result.PadBytes(PointSize);
            return true;
        }
    }
}
