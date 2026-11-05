using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsMapFp2ToG2Precompile : PrecompileHandlerBase
    {
        private const int FieldElementSize = 128;
        private const int PointSize = 256;

        private readonly IBls12381Operations _ops;
        private readonly IBls12381OutcomeOperations _outcomes;

        public BlsMapFp2ToG2Precompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
            _outcomes = ops as IBls12381OutcomeOperations;
        }

        public override int AddressNumeric => 0x11;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, FieldElementSize, "BLS12-381 MAP_FP2_TO_G2");
            return TryExecute(input, out var output)
                ? output
                : throw new ArgumentException("BLS12-381 MAP_FP2_TO_G2: invalid field element encoding");
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
                if (!_outcomes.TryMapFp2ToG2(input, out result))
                {
                    output = null;
                    return false;
                }
            }
            else if (!TryThroughThrowingOperation(() => _ops.MapFp2ToG2(input), out result))
            {
                output = null;
                return false;
            }

            output = result.Length == PointSize ? result : result.PadBytes(PointSize);
            return true;
        }
    }
}
