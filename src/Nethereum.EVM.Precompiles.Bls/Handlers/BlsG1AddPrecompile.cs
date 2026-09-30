using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsG1AddPrecompile : PrecompileHandlerBase
    {
        private const int G1PointSize = 128;
        private const int InputSize = G1PointSize * 2;

        private readonly IBls12381Operations _ops;

        public BlsG1AddPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x0b;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, InputSize, "BLS12-381 G1ADD");

            var p1 = input.Slice(0, G1PointSize);
            var p2 = input.Slice(G1PointSize, InputSize);

            var result = _ops.G1Add(p1, p2);
            return result.Length == G1PointSize ? result : result.PadBytes(G1PointSize);
        }
    }
}
