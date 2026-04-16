using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// MODEXP (0x05) gas calculator per EIP-2565 (Berlin). The formula:
    ///
    /// <code>
    /// iterationCount = expLen &lt;= 32
    ///     ? (expHead == 0 ? 0 : expBitLen - 1)
    ///     : 8 × (expLen - 32) + (expHead &gt; 0 ? expBitLen - 1 : 0)
    /// iterationCount = max(1, iterationCount)
    ///
    /// maxLen = max(baseLen, modLen)
    /// words = ⌈maxLen / 8⌉
    /// mulComplexity = words × words
    /// gas = max(200, mulComplexity × iterationCount / 3)
    /// </code>
    ///
    /// Used by Cancun, Prague and any L2 that wants Berlin-era MODEXP
    /// pricing. Osaka replaces this with
    /// <see cref="Eip7883ModExpGasCalculator"/>. All arithmetic runs on
    /// <see cref="EvmUInt256"/> so no <c>System.Numerics.BigInteger</c>
    /// touches the Nethereum.EVM.Core hot path. Parity
    /// with the legacy BigInteger implementation is enforced by
    /// <c>PrecompileGasCalculatorsTests</c>.
    /// </summary>
    public sealed class Eip2565ModExpGasCalculator : IPrecompileGasCalculator
    {
        public long GetGasCost(byte[] input)
        {
            var hdr = ModExpHeaderParser.Parse(input);

            // Berlin EIP-2565 iteration count: multiplier = 8.
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
                iterationCount = new EvmUInt256(8UL) * expLenMinus32 + extra;
            }
            if (iterationCount.IsZero) iterationCount = EvmUInt256.One;

            var maxLen = hdr.BaseLen > hdr.ModLen ? hdr.BaseLen : hdr.ModLen;
            var words = (maxLen + new EvmUInt256(7UL)) / new EvmUInt256(8UL);

            // Guard: if words is large enough that words^2 would overflow or
            // exceed long.MaxValue, the gas is guaranteed to exceed any gas limit.
            if (words > new EvmUInt256(3037000499UL)) // floor(sqrt(long.MaxValue))
                return long.MaxValue;

            var mulComplexity = words * words;
            var gas = mulComplexity * iterationCount / new EvmUInt256(3UL);

            if (gas < new EvmUInt256(200UL)) gas = new EvmUInt256(200UL);

            return gas.ToLongSafe();
        }
    }
}
