#if NET5_0_OR_GREATER && !MODEXP_PORTABLE
using System;

namespace Nethereum.EVM.Precompiles.Backends.ModExp
{
    internal static class PowerOfTwoModulusExponentiation
    {
        public static ulong[] Power(byte[] baseBytes, ModExpExponent exponent, int modulusBits)
        {
            int length = (modulusBits + 63) / 64;
            ulong topMask = (modulusBits & 63) == 0 ? ulong.MaxValue : (1UL << (modulusBits & 63)) - 1;
            var value = ModExpLimbs.Resize(ModExpLimbs.FromBigEndian(baseBytes), length);
            value[length - 1] &= topMask;
            var accumulator = (ulong[])value.Clone();
            var product = new ulong[length];
            for (int bit = exponent.BitLength - 2; bit >= 0; bit--)
            {
                MultiplyLow(accumulator, accumulator, product);
                Array.Copy(product, accumulator, length);
                if (exponent.Bit(bit))
                {
                    MultiplyLow(accumulator, value, product);
                    Array.Copy(product, accumulator, length);
                }
            }
            accumulator[length - 1] &= topMask;
            return ModExpLimbs.Trim(accumulator);
        }

        private static void MultiplyLow(ulong[] a, ulong[] b, ulong[] result)
        {
            int length = result.Length;
            Array.Clear(result, 0, length);
            for (int i = 0; i < length; i++)
            {
                ulong carry = 0;
                for (int j = 0; i + j < length; j++)
                {
                    ulong high = Math.BigMul(a[i], b[j], out ulong product);
                    product += result[i + j];
                    high += product < result[i + j] ? 1UL : 0UL;
                    product += carry;
                    high += product < carry ? 1UL : 0UL;
                    result[i + j] = product;
                    carry = high;
                }
            }
        }
    }
}

#endif
