using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsMapFpToG1Precompile : PrecompileHandlerBase
    {
        private const int FieldElementSize = 64;
        private const int PointSize = 128;

        private readonly IBls12381Operations _ops;
        private readonly IBls12381OutcomeOperations _outcomes;

        public BlsMapFpToG1Precompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
            _outcomes = ops as IBls12381OutcomeOperations;
        }

        public override int AddressNumeric => 0x10;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, FieldElementSize, "BLS12-381 MAP_FP_TO_G1");
            return TryExecute(input, out var output)
                ? output
                : throw new ArgumentException("BLS12-381 MAP_FP_TO_G1: invalid field element encoding");
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            if (!HasInputLength(input, FieldElementSize))
            {
                output = null;
                return false;
            }

            byte[] result;
            if (_outcomes != null)
            {
                if (!_outcomes.TryMapFpToG1(input, out result))
                {
                    output = null;
                    return false;
                }
            }
            else if (!TryThroughThrowingOperation(() => _ops.MapFpToG1(input), out result))
            {
                output = null;
                return false;
            }

            output = result.Length == PointSize ? result : result.PadBytes(PointSize);
            return true;
        }
    }
}
