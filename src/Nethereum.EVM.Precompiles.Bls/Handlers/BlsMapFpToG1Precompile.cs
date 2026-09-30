using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    public sealed class BlsMapFpToG1Precompile : PrecompileHandlerBase
    {
        private const int FpSize = 64;
        private const int G1PointSize = 128;

        private readonly IBls12381Operations _ops;

        public BlsMapFpToG1Precompile(IBls12381Operations ops)
        {
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        public override int AddressNumeric => 0x10;

        public override byte[] Execute(byte[] input)
        {
            RequireInputLength(input, FpSize, "BLS12-381 MAP_FP_TO_G1");

            var result = _ops.MapFpToG1(input);
            return result.Length == G1PointSize ? result : result.PadBytes(G1PointSize);
        }
    }
}
