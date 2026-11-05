using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal static class LimbArithmetic
    {
        public static ulong AddWithCarry(ulong a, ulong b, ulong carryIn, out ulong sum)
        {
            var partial = a + b;
            var carryOut = partial < a ? 1UL : 0UL;
            sum = partial + carryIn;
            return carryOut | (sum < partial ? 1UL : 0UL);
        }

        public static ulong SubtractWithBorrow(ulong a, ulong b, ulong borrowIn, out ulong difference)
        {
            var partial = a - b;
            var borrowOut = a < b ? 1UL : 0UL;
            difference = partial - borrowIn;
            return borrowOut | (partial < borrowIn ? 1UL : 0UL);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Multiply(ulong a, ulong b, out ulong low)
        {
#if NET5_0_OR_GREATER && !BN254_PORTABLE_MULTIPLY
            return Math.BigMul(a, b, out low);
#else
            const ulong halfMask = 0xFFFFFFFFUL;
            var aLow = a & halfMask;
            var aHigh = a >> 32;
            var bLow = b & halfMask;
            var bHigh = b >> 32;
            var lowProduct = aLow * bLow;
            var crossA = aLow * bHigh;
            var crossB = aHigh * bLow;
            var middle = (lowProduct >> 32) + (crossA & halfMask) + (crossB & halfMask);
            low = (middle << 32) | (lowProduct & halfMask);
            return aHigh * bHigh + (crossA >> 32) + (crossB >> 32) + (middle >> 32);
#endif
        }
    }
}
