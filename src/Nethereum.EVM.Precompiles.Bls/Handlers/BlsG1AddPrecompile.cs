using System;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.Signer.Bls;
using Nethereum.Util;

namespace Nethereum.EVM.Precompiles.Bls.Handlers
{
    /// <summary>
    /// Precompile 0x0b — BLS12-381 G1ADD (EIP-2537). Added in Prague.
    /// Adds two G1 points on the BLS12-381 curve.
    ///
    /// Input: 256 bytes (two G1 points, 128 bytes each in the EIP-2537
    /// padded encoding — 64 bytes X || 64 bytes Y per point).
    /// Output: 128 bytes (one G1 point).
    ///
    /// Backend is injected at ctor time via <see cref="IBls12381Operations"/>;
    /// this handler class contains no crypto itself. Production wires in
    /// <c>Nethereum.Signer.Bls.Herumi.Bls12381Operations</c> (MCL/Herumi).
    /// Gas cost (375 per EIP-2537) lives on the fork's
    /// <see cref="PrecompileGasCalculators"/>.
    /// </summary>
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
