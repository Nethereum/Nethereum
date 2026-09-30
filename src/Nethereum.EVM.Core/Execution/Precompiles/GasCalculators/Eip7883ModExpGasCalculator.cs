using Nethereum.Util;

namespace Nethereum.EVM.Execution.Precompiles.GasCalculators
{
    public sealed class Eip7883ModExpGasCalculator : IPrecompileGasCalculator
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
                iterationCount = new EvmUInt256(16UL) * expLenMinus32 + extra;
            }
            if (iterationCount.IsZero) iterationCount = EvmUInt256.One;

            var maxLen = hdr.BaseLen > hdr.ModLen ? hdr.BaseLen : hdr.ModLen;
            var words = (maxLen + new EvmUInt256(7UL)) / new EvmUInt256(8UL);
            var mulComplexity = maxLen <= new EvmUInt256(32UL)
                ? new EvmUInt256(16UL)
                : new EvmUInt256(2UL) * words * words;

            var gas = mulComplexity * iterationCount;
            if (gas < new EvmUInt256(500UL)) gas = new EvmUInt256(500UL);

            return gas.ToLongSafe();
        }
    }
}
