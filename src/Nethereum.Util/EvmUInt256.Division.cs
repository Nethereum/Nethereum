#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
using Limbs = System.Span<ulong>;
#else
using Limbs = System.UInt64[];
#endif
using System;
using System.Runtime.CompilerServices;

namespace Nethereum.Util
{
    public readonly partial struct EvmUInt256
    {
        private static readonly ushort[] ReciprocalSeeds = CreateReciprocalSeeds();

        private static ushort[] CreateReciprocalSeeds()
        {
            var seeds = new ushort[256];
            for (int i = 0; i < seeds.Length; i++)
                seeds[i] = (ushort)(((1 << 19) - 3 * (1 << 8)) / (256 + i));
            return seeds;
        }

        private static int LimbLength(EvmUInt256 value)
        {
            return value._u3 != 0 ? 4 : value._u2 != 0 ? 3 : value._u1 != 0 ? 2 : value._u0 != 0 ? 1 : 0;
        }

        private static void Reduce(EvmUInt256 low, EvmUInt256 high, EvmUInt256 divisor, out EvmUInt256 quotient, out EvmUInt256 remainder)
        {
            int divisorLength = LimbLength(divisor);
            int numeratorLength = high.IsZero ? LimbLength(low) : 4 + LimbLength(high);

            if (numeratorLength < divisorLength)
            {
                quotient = Zero;
                remainder = low;
                return;
            }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            Span<ulong> shiftedNumerator = stackalloc ulong[9];
            Span<ulong> quotientLimbs = stackalloc ulong[8];
            Span<ulong> shiftedDivisor = stackalloc ulong[5];
#else
            var shiftedNumerator = new ulong[9];
            var quotientLimbs = new ulong[8];
            var shiftedDivisor = new ulong[5];
#endif
            shiftedNumerator[0] = low._u0; shiftedNumerator[1] = low._u1; shiftedNumerator[2] = low._u2; shiftedNumerator[3] = low._u3;
            shiftedNumerator[4] = high._u0; shiftedNumerator[5] = high._u1; shiftedNumerator[6] = high._u2; shiftedNumerator[7] = high._u3;
            shiftedDivisor[0] = divisor._u0; shiftedDivisor[1] = divisor._u1; shiftedDivisor[2] = divisor._u2; shiftedDivisor[3] = divisor._u3;

            int shift = 64 - BitLen64(shiftedDivisor[divisorLength - 1]);
            ShiftLeft(shiftedDivisor, divisorLength, shift);
            ShiftLeft(shiftedNumerator, numeratorLength, shift);

            if (divisorLength == 1)
            {
                ulong lastRemainder = DivideByLimb(shiftedNumerator, numeratorLength + 1, shiftedDivisor[0], quotientLimbs);
                remainder = new EvmUInt256(lastRemainder >> shift);
            }
            else
            {
                DivideByLimbs(shiftedNumerator, numeratorLength + 1, shiftedDivisor, divisorLength, quotientLimbs);
                remainder = ShiftedRemainder(shiftedNumerator, divisorLength, shift);
            }
            quotient = new EvmUInt256(quotientLimbs[3], quotientLimbs[2], quotientLimbs[1], quotientLimbs[0]);
        }

        private static void ShiftLeft(Limbs limbs, int length, int shift)
        {
            int complement = 63 - shift;
            for (int i = length; i > 0; i--)
                limbs[i] = (limbs[i] << shift) | ((limbs[i - 1] >> 1) >> complement);
            limbs[0] <<= shift;
        }

        private static EvmUInt256 ShiftedRemainder(Limbs shiftedNumerator, int length, int shift)
        {
            return new EvmUInt256(
                length > 3 ? ShiftedRightWord(shiftedNumerator, 3, shift) : 0,
                length > 2 ? ShiftedRightWord(shiftedNumerator, 2, shift) : 0,
                length > 1 ? ShiftedRightWord(shiftedNumerator, 1, shift) : 0,
                ShiftedRightWord(shiftedNumerator, 0, shift));
        }

        private static ulong ShiftedRightWord(Limbs shifted, int index, int shift)
        {
            return (shifted[index] >> shift) | ((shifted[index + 1] << 1) << (63 - shift));
        }

        private static ulong DivideByLimb(Limbs shiftedNumerator, int length, ulong divisor, Limbs quotient)
        {
            ulong reciprocal = Reciprocal(divisor);
            ulong remainder = shiftedNumerator[length - 1];
            for (int j = length - 2; j >= 0; j--)
                quotient[j] = DivideTwoWordsByOne(remainder, shiftedNumerator[j], divisor, reciprocal, out remainder);
            return remainder;
        }

        private static void DivideByLimbs(Limbs shiftedNumerator, int length, Limbs divisor, int divisorLength, Limbs quotient)
        {
            ulong high = divisor[divisorLength - 1];
            ulong next = divisor[divisorLength - 2];
            ulong reciprocal = Reciprocal(high);

            for (int j = length - divisorLength - 1; j >= 0; j--)
            {
                ulong u2 = shiftedNumerator[j + divisorLength];
                ulong u1 = shiftedNumerator[j + divisorLength - 1];
                ulong u0 = shiftedNumerator[j + divisorLength - 2];

                ulong estimate;
                if (u2 >= high)
                {
                    estimate = ulong.MaxValue;
                }
                else
                {
                    estimate = DivideTwoWordsByOne(u2, u1, high, reciprocal, out ulong estimateRemainder);
                    Mul64(estimate, next, out ulong productLow, out ulong productHigh);
                    if (productHigh > estimateRemainder || (productHigh == estimateRemainder && productLow > u0))
                        estimate--;
                }

                ulong borrow = SubtractMultiple(shiftedNumerator, j, divisor, divisorLength, estimate);
                shiftedNumerator[j + divisorLength] = u2 - borrow;
                if (u2 < borrow)
                {
                    estimate--;
                    shiftedNumerator[j + divisorLength] += AddBack(shiftedNumerator, j, divisor, divisorLength);
                }
                quotient[j] = estimate;
            }
        }

        private static ulong SubtractMultiple(Limbs target, int offset, Limbs subtrahend, int length, ulong multiplier)
        {
            if (length == 4) return SubtractMultipleOfFourLimbs(target, offset, subtrahend, multiplier);
            ulong borrow = 0;
            for (int i = 0; i < length; i++)
            {
                ulong word = target[offset + i];
                ulong afterBorrow = word - borrow;
                ulong borrowOut = word < borrow ? 1UL : 0UL;
                Mul64(subtrahend[i], multiplier, out ulong productLow, out ulong productHigh);
                target[offset + i] = afterBorrow - productLow;
                borrow = productHigh + borrowOut + (afterBorrow < productLow ? 1UL : 0UL);
            }
            return borrow;
        }

        private static ulong SubtractMultipleOfFourLimbs(Limbs target, int offset, Limbs subtrahend, ulong multiplier)
        {
            Mul64(subtrahend[0], multiplier, out ulong product0, out ulong carry0);
            Mul64(subtrahend[1], multiplier, out ulong low1, out ulong carry1);
            Mul64(subtrahend[2], multiplier, out ulong low2, out ulong carry2);
            Mul64(subtrahend[3], multiplier, out ulong low3, out ulong product4);

            ulong product1 = low1 + carry0;
            ulong carry = product1 < low1 ? 1UL : 0UL;
            ulong product2 = low2 + carry1;
            ulong carryOut = product2 < low2 ? 1UL : 0UL;
            product2 += carry;
            carry = carryOut + (product2 < carry ? 1UL : 0UL);
            ulong product3 = low3 + carry2;
            carryOut = product3 < low3 ? 1UL : 0UL;
            product3 += carry;
            carry = carryOut + (product3 < carry ? 1UL : 0UL);
            product4 += carry;

            ulong word0 = target[offset];
            ulong word1 = target[offset + 1];
            ulong word2 = target[offset + 2];
            ulong word3 = target[offset + 3];

            target[offset] = word0 - product0;
            ulong borrow = word0 < product0 ? 1UL : 0UL;
            ulong difference = word1 - product1;
            ulong borrowOut = word1 < product1 ? 1UL : 0UL;
            target[offset + 1] = difference - borrow;
            borrow = borrowOut + (difference < borrow ? 1UL : 0UL);
            difference = word2 - product2;
            borrowOut = word2 < product2 ? 1UL : 0UL;
            target[offset + 2] = difference - borrow;
            borrow = borrowOut + (difference < borrow ? 1UL : 0UL);
            difference = word3 - product3;
            borrowOut = word3 < product3 ? 1UL : 0UL;
            target[offset + 3] = difference - borrow;
            borrow = borrowOut + (difference < borrow ? 1UL : 0UL);
            return product4 + borrow;
        }

        private static ulong AddBack(Limbs target, int offset, Limbs addend, int length)
        {
            ulong carry = 0;
            for (int i = 0; i < length; i++)
            {
                ulong word = target[offset + i];
                ulong sum = word + carry;
                ulong carryOut = sum < carry ? 1UL : 0UL;
                sum += addend[i];
                carryOut += sum < addend[i] ? 1UL : 0UL;
                target[offset + i] = sum;
                carry = carryOut;
            }
            return carry;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong DivideTwoWordsByOne(ulong high, ulong low, ulong divisor, ulong reciprocal, out ulong remainder)
        {
            Mul64(reciprocal, high, out ulong quotientLow, out ulong quotientHigh);
            quotientLow += low;
            quotientHigh += high + (quotientLow < low ? 1UL : 0UL);
            quotientHigh++;
            ulong r = low - quotientHigh * divisor;
            if (r > quotientLow)
            {
                quotientHigh--;
                r += divisor;
            }
            if (r >= divisor)
            {
                quotientHigh++;
                r -= divisor;
            }
            remainder = r;
            return quotientHigh;
        }

        private static ulong Reciprocal(ulong divisor)
        {
            ulong oddBit = divisor & 1;
            ulong divisor40 = (divisor >> 24) + 1;
            ulong divisor63 = (divisor >> 1) + oddBit;

            ulong v0 = ReciprocalSeeds[(int)(divisor >> 55) - 256];
            ulong v1 = (v0 << 11) - ((v0 * v0 * divisor40) >> 40) - 1;
            ulong v2 = (v1 << 13) + ((v1 * ((1UL << 60) - v1 * divisor40)) >> 47);
            ulong error = ((v2 >> 1) & (0UL - oddBit)) - v2 * divisor63;
            Mul64(v2, error, out _, out ulong errorHigh);
            ulong v3 = (v2 << 31) + (errorHigh >> 1);

            Mul64(v3, divisor, out ulong productLow, out ulong productHigh);
            ulong carry = productLow + divisor < productLow ? 1UL : 0UL;
            return v3 - (productHigh + carry + divisor);
        }
    }
}
