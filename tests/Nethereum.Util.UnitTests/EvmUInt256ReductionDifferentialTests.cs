using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256ReductionDifferentialTests
    {
        private const int AlwaysOnCases = 20_000;
        private const int FullCases = 1_000_000;
        private const int MaxReportedFailures = 5;
        private static readonly BigInteger Two256 = BigInteger.One << 256;

        [Fact]
        public void Given_seeded_random_operands_When_dividing_adding_and_multiplying_modulo_Then_results_equal_the_BigInteger_reference()
        {
            AssertAgainstReference(AlwaysOnCases, seed: 20261021);
        }

        [Fact]
        [Trait("Category", "LargeFixture")]
        public void Given_one_million_seeded_operands_When_dividing_adding_and_multiplying_modulo_Then_results_equal_the_BigInteger_reference()
        {
            AssertAgainstReference(FullCases, seed: 7);
        }

        [Fact]
        public void Given_every_pair_of_edge_values_When_dividing_and_taking_remainders_Then_results_equal_the_BigInteger_reference()
        {
            var failures = new Failures();
            foreach (var a in EdgeValues())
                foreach (var b in EdgeValues())
                    CheckDivision(a, b, failures);

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_every_triple_of_edge_values_When_adding_and_multiplying_modulo_Then_results_equal_the_BigInteger_reference()
        {
            var failures = new Failures();
            var edges = EdgeValues();
            foreach (var a in edges)
                foreach (var b in edges)
                    foreach (var m in edges)
                        CheckModular(a, b, m, failures);

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_a_zero_modulus_When_adding_or_multiplying_modulo_Then_the_result_is_zero()
        {
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.AddMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, EvmUInt256.Zero));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.MulMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, EvmUInt256.Zero));
        }

        [Fact]
        public void Given_a_zero_divisor_When_dividing_or_taking_the_remainder_Then_the_result_is_zero()
        {
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.MaxValue / EvmUInt256.Zero);
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.MaxValue % EvmUInt256.Zero);
        }

        [Fact]
        public void Given_a_modulus_of_one_When_adding_or_multiplying_modulo_Then_the_result_is_zero()
        {
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.AddMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, EvmUInt256.One));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.MulMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, EvmUInt256.One));
        }

        [Fact]
        public void Given_a_sum_that_overflows_256_bits_When_adding_modulo_Then_the_carry_is_part_of_the_reduced_value()
        {
            var modulus = new EvmUInt256(10);
            var result = EvmUInt256.AddMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, modulus);
            Assert.Equal((BigInteger)((Two256 - 1) * 2 % 10), (BigInteger)result);
        }

        [Fact]
        public void Given_divisors_whose_top_limb_sits_on_the_reciprocal_boundaries_When_dividing_and_multiplying_modulo_Then_results_equal_the_BigInteger_reference()
        {
            var random = new System.Random(64);
            var failures = new Failures();
            foreach (var topLimb in TopLimbsAroundTheReciprocalBoundaries())
                for (int limbs = 1; limbs <= 4; limbs++)
                {
                    var divisor = DivisorWithTopLimb(random, topLimb, limbs);
                    var dividend = Operand(random);
                    CheckDivision(dividend, divisor, failures);
                    CheckModular(dividend, Operand(random), divisor, failures);
                }

            Assert.Equal(string.Empty, failures.ToString());
        }

        private static IEnumerable<ulong> TopLimbsAroundTheReciprocalBoundaries()
        {
            for (ulong offset = 0; offset < 600; offset++)
            {
                yield return (1UL << 63) + offset;
                yield return ulong.MaxValue - offset;
            }
            for (int bit = 0; bit < 63; bit++)
            {
                yield return (1UL << 63) | (1UL << bit);
                yield return ulong.MaxValue ^ (1UL << bit);
            }
            for (ulong seed = 0; seed < 512; seed++)
                yield return (1UL << 63) | (seed << 55) | (seed * 0x9E3779B97F4A7C15UL >> 9);
        }

        private static BigInteger DivisorWithTopLimb(System.Random random, ulong topLimb, int limbs)
        {
            var bytes = new byte[33];
            random.NextBytes(bytes);
            bytes[32] = 0;
            for (int i = 0; i < 8; i++)
                bytes[(limbs - 1) * 8 + i] = (byte)(topLimb >> (8 * i));
            for (int i = limbs * 8; i < 32; i++)
                bytes[i] = 0;
            return new BigInteger(bytes);
        }

        private static void AssertAgainstReference(int count, int seed)
        {
            var random = new System.Random(seed);
            var failures = new Failures();
            for (int i = 0; i < count; i++)
            {
                var a = Operand(random);
                var b = Operand(random);
                var m = Operand(random);
                CheckDivision(a, b, failures);
                CheckModular(a, b, m, failures);
            }
            Assert.Equal(string.Empty, failures.ToString());
        }

        private static void CheckDivision(BigInteger a, BigInteger b, Failures failures)
        {
            var x = (EvmUInt256)a;
            var y = (EvmUInt256)b;
            var expectedQuotient = b.IsZero ? BigInteger.Zero : a / b;
            var expectedRemainder = b.IsZero ? BigInteger.Zero : a % b;
            failures.Compare("DIV", expectedQuotient, (BigInteger)(x / y), a, b);
            failures.Compare("MOD", expectedRemainder, (BigInteger)(x % y), a, b);
            EvmUInt256.Divmod(x, y, out var quotient, out var remainder);
            failures.Compare("DIVMOD quotient", expectedQuotient, (BigInteger)quotient, a, b);
            failures.Compare("DIVMOD remainder", expectedRemainder, (BigInteger)remainder, a, b);
        }

        private static void CheckModular(BigInteger a, BigInteger b, BigInteger m, Failures failures)
        {
            var x = (EvmUInt256)a;
            var y = (EvmUInt256)b;
            var z = (EvmUInt256)m;
            failures.Compare("ADDMOD", m.IsZero ? BigInteger.Zero : (a + b) % m, (BigInteger)EvmUInt256.AddMod(x, y, z), a, b, m);
            failures.Compare("MULMOD", m.IsZero ? BigInteger.Zero : (a * b) % m, (BigInteger)EvmUInt256.MulMod(x, y, z), a, b, m);
        }

        private static BigInteger Operand(System.Random random)
        {
            int limbs = random.Next(1, 5);
            var bytes = new byte[33];
            for (int limb = 0; limb < limbs; limb++)
            {
                ulong word = Word(random);
                for (int i = 0; i < 8; i++)
                    bytes[limb * 8 + i] = (byte)(word >> (8 * i));
            }
            return new BigInteger(bytes);
        }

        private static ulong Word(System.Random random)
        {
            switch (random.Next(8))
            {
                case 0: return 0;
                case 1: return ulong.MaxValue;
                case 2: return 1UL << random.Next(64);
                case 3: return (1UL << random.Next(1, 64)) - 1;
                case 4: return 0x8000000000000000UL | (ulong)random.Next();
                case 5: return ulong.MaxValue - (ulong)random.Next(4);
                default:
                    var raw = new byte[8];
                    random.NextBytes(raw);
                    return BitConverter.ToUInt64(raw, 0);
            }
        }

        private static List<BigInteger> EdgeValues()
        {
            var values = new List<BigInteger>
            {
                0, 1, 2, 3, 10, 255, 256,
                ulong.MaxValue, (BigInteger)ulong.MaxValue + 1, (BigInteger)ulong.MaxValue + 2,
                (BigInteger.One << 128) - 1, BigInteger.One << 128, (BigInteger.One << 128) + 1,
                (BigInteger.One << 192) - 1, BigInteger.One << 192, (BigInteger.One << 192) + 1,
                (BigInteger.One << 255) - 1, BigInteger.One << 255, (BigInteger.One << 255) + 1,
                Two256 - 1, Two256 - 2, Two256 - (BigInteger.One << 128), Two256 - (BigInteger.One << 64),
                BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F", System.Globalization.NumberStyles.HexNumber),
                BigInteger.Parse("0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141", System.Globalization.NumberStyles.HexNumber),
                BigInteger.Parse("100000000000000000000000000000033", System.Globalization.NumberStyles.HexNumber),
                BigInteger.Parse("10000000000000033", System.Globalization.NumberStyles.HexNumber),
                BigInteger.Parse("0FFFFFFFFFFFFFFFF0000000000000000FFFFFFFFFFFFFFFF0000000000000001", System.Globalization.NumberStyles.HexNumber),
                BigInteger.Parse("08000000000000000FFFFFFFFFFFFFFFF8000000000000000FFFFFFFFFFFFFFFF", System.Globalization.NumberStyles.HexNumber)
            };
            for (int bit = 1; bit < 256; bit += 17)
            {
                values.Add(BigInteger.One << bit);
                values.Add((BigInteger.One << bit) - 1);
            }
            return values;
        }

        private sealed class Failures
        {
            private readonly StringBuilder _text = new StringBuilder();
            private int _count;

            public void Compare(string operation, BigInteger expected, BigInteger actual, params BigInteger[] operands)
            {
                if (expected == actual) return;
                if (_count++ >= MaxReportedFailures) return;
                _text.Append(operation).Append(" operands=[").Append(string.Join(", ", operands)).Append("] expected=")
                    .Append(expected).Append(" actual=").Append(actual).AppendLine();
            }

            public override string ToString() => _count > MaxReportedFailures
                ? _text + $"(+{_count - MaxReportedFailures} more)"
                : _text.ToString();
        }
    }
}
