using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// MODEXP (0x05) gas calculator per EIP-7883 (Osaka). Doubles the
    /// iteration-count multiplier from 8 to 16, changes the complexity
    /// of squaring to <c>2 × words²</c> (or a flat 16 when <c>maxLen ≤ 32</c>),
    /// drops the division by 3, and raises the floor from 200 to 500.
    /// EIP-7823's 1024-byte bound on base/exp/mod length is enforced on
    /// the handler side (<c>ModExpPrecompile.Execute</c>) not here.
    ///
    /// All arithmetic runs on <see cref="EvmUInt256"/>. Parity
    /// with the legacy BigInteger implementation is enforced by
    /// <c>PrecompileGasCalculatorsTests</c>.
    /// </summary>
    public sealed class Eip7883ModExpGasCalculator : IPrecompileGasCalculator
    {
        public long GetGasCost(byte[] input)
        {
            var hdr = ModExpHeaderParser.Parse(input);

            // EIP-7883: multiplier is 16 (double Berlin's 8).
            EvmUInt256 iterationCount;
            if (hdr.ExpLen <= 32 && hdr.ExpHead.IsZero)
            {
                iterationCount = EvmUInt256.Zero;
            }
            else if (hdr.ExpLen <= 32)
            {
                iterationCount = new EvmUInt256((ulong)(hdr.ExpBitLen - 1));
            }
            else
            {
                var expLenMinus32 = hdr.ExpLen - new EvmUInt256(32UL);
                var extra = hdr.ExpHead.IsZero
                    ? EvmUInt256.Zero
                    : new EvmUInt256((ulong)(hdr.ExpBitLen - 1));
                iterationCount = new EvmUInt256(16UL) * expLenMinus32 + extra;
            }
            if (iterationCount.IsZero) iterationCount = EvmUInt256.One;

            // EIP-7883: mulComplexity = 16 when maxLen ≤ 32, else 2 × words².
            var maxLen = hdr.BaseLen > hdr.ModLen ? hdr.BaseLen : hdr.ModLen;
            var words = (maxLen + new EvmUInt256(7UL)) / new EvmUInt256(8UL);
            var mulComplexity = maxLen <= new EvmUInt256(32UL)
                ? new EvmUInt256(16UL)
                : new EvmUInt256(2UL) * words * words;

            // EIP-7883: no division by 3, floor rises to 500.
            var gas = mulComplexity * iterationCount;
            if (gas < new EvmUInt256(500UL)) gas = new EvmUInt256(500UL);

            return gas.ToLongSafe();
        }
    }
}
