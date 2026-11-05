using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsG1AddPrecompile : PrecompileHandlerBase
    {
        private const int PointSize = 128;
        private const int InputSize = PointSize * 2;

        private readonly IBls12381Operations _ops;
        private readonly IBls12381OutcomeOperations _outcomes;

        public BlsG1AddPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
            _outcomes = ops as IBls12381OutcomeOperations;
        }

        public override int AddressNumeric => 0x0b;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, InputSize, "BLS12-381 G1ADD");
            return TryExecute(input, out var output)
                ? output
                : throw new ArgumentException("BLS12-381 G1ADD: invalid point encoding");
        }

        public override bool TryExecute(byte[] input, out byte[] output)
        {
            if (!HasInputLength(input, InputSize))
            {
                output = null;
                return false;
            }

            var p1 = input.Slice(0, PointSize);
            var p2 = input.Slice(PointSize, InputSize);

            byte[] result;
            if (_outcomes != null)
            {
                if (!_outcomes.TryG1Add(p1, p2, out result))
                {
                    output = null;
                    return false;
                }
            }
            else if (!TryThroughThrowingOperation(() => _ops.G1Add(p1, p2), out result))
            {
                output = null;
                return false;
            }

            output = result.Length == PointSize ? result : result.PadBytes(PointSize);
            return true;
        }
    }
}
