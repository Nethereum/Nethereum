using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsG2AddPrecompile : PrecompileHandlerBase
    {
        private const int G2PointSize = 256;
        private const int InputSize = G2PointSize * 2;

        private readonly IBls12381Operations _ops;

        public BlsG2AddPrecompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x0d;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, InputSize, "BLS12-381 G2ADD");

            var p1 = input.Slice(0, G2PointSize);
            var p2 = input.Slice(G2PointSize, InputSize);

            var result = _ops.G2Add(p1, p2);
            return result.Length == G2PointSize ? result : result.PadBytes(G2PointSize);
        }
    }
}
