using System;
using System.Numerics;

namespace Nethereum.Signer.Crypto.BN128.Managed
{
    internal static class G1ScalarMultiplication
    {
        private const int WindowBits = 4;
        private const int OddMultipleCount = 1 << (WindowBits - 2);

        public static G1Jacobian Multiply(G1Jacobian point, BigInteger scalar)
        {
            var reduced = scalar % Bn254Constants.Order;
            if (reduced.IsZero || point.IsInfinity) return G1Jacobian.Infinity;

            Decompose(reduced, out var k1, out var k2);
            var plainMultiples = OddMultiples(point);
            var endomorphismMultiples = Map(plainMultiples, multiple => multiple.ApplyEndomorphism());
            return Interleave(plainMultiples, SignedDigits(k1), endomorphismMultiples, SignedDigits(k2));
        }

        public static void Decompose(BigInteger scalar, out BigInteger k1, out BigInteger k2)
        {
            var c1 = DivideRounded(scalar * Bn254Constants.LatticeBasis2Y, Bn254Constants.Order);
            var c2 = DivideRounded(-scalar * Bn254Constants.LatticeBasis1Y, Bn254Constants.Order);
            k1 = scalar - c1 * Bn254Constants.LatticeBasis1X - c2 * Bn254Constants.LatticeBasis2X;
            k2 = -c1 * Bn254Constants.LatticeBasis1Y - c2 * Bn254Constants.LatticeBasis2Y;
        }

        private static G1Jacobian[] OddMultiples(G1Jacobian point)
        {
            var multiples = new G1Jacobian[OddMultipleCount];
            var twice = point.Double();
            multiples[0] = point;
            for (var i = 1; i < OddMultipleCount; i++) multiples[i] = multiples[i - 1].Add(twice);
            return multiples;
        }

        private static G1Jacobian[] Map(G1Jacobian[] source, Func<G1Jacobian, G1Jacobian> transform)
        {
            var mapped = new G1Jacobian[source.Length];
            for (var i = 0; i < source.Length; i++) mapped[i] = transform(source[i]);
            return mapped;
        }

        private static int[] SignedDigits(BigInteger scalar)
        {
            var sign = scalar.Sign < 0 ? -1 : 1;
            var remaining = BigInteger.Abs(scalar);
            var digits = new int[BigEndianBytes.BitLength(remaining) + 1];
            var window = 1 << WindowBits;
            for (var i = 0; !remaining.IsZero; i++)
            {
                if (!remaining.IsEven)
                {
                    var digit = (int)(remaining & (window - 1));
                    if (digit >= window / 2) digit -= window;
                    remaining -= digit;
                    digits[i] = sign * digit;
                }
                remaining >>= 1;
            }
            return digits;
        }

        private static G1Jacobian Interleave(G1Jacobian[] firstMultiples, int[] firstDigits, G1Jacobian[] secondMultiples, int[] secondDigits)
        {
            var result = G1Jacobian.Infinity;
            for (var i = Math.Max(firstDigits.Length, secondDigits.Length) - 1; i >= 0; i--)
            {
                result = result.Double();
                result = AddDigit(result, firstMultiples, DigitAt(firstDigits, i));
                result = AddDigit(result, secondMultiples, DigitAt(secondDigits, i));
            }
            return result;
        }

        private static int DigitAt(int[] digits, int index) => index < digits.Length ? digits[index] : 0;

        private static G1Jacobian AddDigit(G1Jacobian accumulator, G1Jacobian[] multiples, int digit)
        {
            if (digit == 0) return accumulator;
            return digit > 0
                ? accumulator.Add(multiples[digit >> 1])
                : accumulator.Add(multiples[(-digit) >> 1].Negate());
        }

        private static BigInteger DivideRounded(BigInteger numerator, BigInteger denominator)
        {
            var doubled = numerator * 2 + denominator;
            var quotient = BigInteger.DivRem(doubled, denominator * 2, out var remainder);
            return remainder.Sign < 0 ? quotient - 1 : quotient;
        }
    }
}
