using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    /// <summary>
    /// MODEXP (0x05) gas calculator per EIP-198 (Byzantium through Istanbul).
    ///
    /// mulComplexity:
    ///   if maxLen &lt;= 64: maxLen^2
    ///   elif maxLen &lt;= 1024: maxLen^2/4 + 96*maxLen - 3072
    ///   else: maxLen^2/16 + 480*maxLen - 199680
    ///
    /// gas = mulComplexity * max(iterationCount, 1) / 20
    /// </summary>
    public sealed class Eip198ModExpGasCalculator : IPrecompileGasCalculator
    {
        public long GetGasCost(byte[] input)
        {
            var hdr = ModExpHeaderParser.Parse(input);

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
            var mulComplexity = CalculateMulComplexity(maxLen);
            var gas = mulComplexity * iterationCount / new EvmUInt256(20UL);

            return gas.ToLongSafe();
        }

        private static EvmUInt256 CalculateMulComplexity(EvmUInt256 x)
        {
            if (x > new EvmUInt256(3037000499UL))
                return new EvmUInt256((ulong)long.MaxValue);
            if (x <= new EvmUInt256(64))
            {
                return x * x;
            }
            else if (x <= new EvmUInt256(1024))
            {
                return x * x / new EvmUInt256(4) + new EvmUInt256(96) * x - new EvmUInt256(3072);
            }
            else
            {
                return x * x / new EvmUInt256(16) + new EvmUInt256(480) * x - new EvmUInt256(199680);
            }
        }
    }
}
