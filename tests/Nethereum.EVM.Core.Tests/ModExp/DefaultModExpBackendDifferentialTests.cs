using System;
using System.Numerics;
using System.Text;
using Nethereum.EVM.Precompiles.Backends;
using Xunit;

namespace Nethereum.EVM.Core.Tests.ModExp
{
    public class DefaultModExpBackendDifferentialTests
    {
        private const int AlwaysOnCases = 20000;
        private const int FullCases = 1000000;
        private const int Seed = 20261022;

        private static readonly DefaultModExpBackend Backend = DefaultModExpBackend.Instance;

        [Fact]
        public void Given_random_operands_When_the_backend_executes_Then_outputs_equal_BigInteger_ModPow()
        {
            AssertIdentical(AlwaysOnCases);
        }

        [Fact]
        [Trait("Category", "ModExpDifferential")]
        [Trait("Category", "LargeFixture")]
        public void Given_one_million_random_operands_When_the_backend_executes_Then_outputs_equal_BigInteger_ModPow()
        {
            AssertIdentical(FullCases);
        }

        [Fact]
        public void Given_the_edge_operands_When_the_backend_executes_Then_outputs_equal_BigInteger_ModPow()
        {
            var failures = new StringBuilder();
            foreach (var baseBytes in EdgeOperands())
                foreach (var exponent in EdgeOperands())
                    foreach (var modulus in EdgeModuli())
                        Compare(baseBytes, exponent, modulus, failures);

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_very_long_operands_When_the_backend_executes_Then_outputs_equal_BigInteger_ModPow()
        {
            var rng = new Random(Seed);
            var failures = new StringBuilder();
            foreach (var modulusLength in new[] { 128, 256, 512, 1024 })
                foreach (var parity in new[] { 0, 1, 2 })
                {
                    var modulus = Modulus(rng, modulusLength, parity);
                    Compare(Random(rng, modulusLength + 17), Random(rng, 37), modulus, failures);
                    Compare(Random(rng, modulusLength), Random(rng, 5), modulus, failures);
                }

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_a_modulus_of_zero_When_the_backend_executes_Then_the_output_is_zero_of_the_modulus_length()
        {
            Assert.Equal(new byte[3], Backend.ModExp(new byte[] { 5 }, new byte[] { 7 }, new byte[3]));
        }

        private static void AssertIdentical(int cases)
        {
            var rng = new Random(Seed);
            var failures = new StringBuilder();
            for (int i = 0; i < cases && failures.Length < 4000; i++)
            {
                var modulus = Modulus(rng, 1 + rng.Next(72), rng.Next(3));
                Compare(Random(rng, rng.Next(90)), Random(rng, rng.Next(40)), modulus, failures);
            }

            Assert.Equal(string.Empty, failures.ToString());
        }

        private static byte[] Random(Random rng, int length)
        {
            var bytes = new byte[length];
            rng.NextBytes(bytes);
            if (length > 0 && rng.Next(4) == 0) bytes[0] = 0;
            if (length > 1 && rng.Next(8) == 0) Array.Clear(bytes, 0, length / 2);
            return bytes;
        }

        private static byte[] Modulus(Random rng, int length, int parity)
        {
            var modulus = Random(rng, length);
            if (parity == 0) modulus[length - 1] |= 1;
            else if (parity == 1) modulus[length - 1] &= 0xfe;
            else modulus[length - 1] = (byte)(1 << rng.Next(8));
            if (parity == 2 && length > 1) Array.Clear(modulus, length - 1 - rng.Next(Math.Min(length - 1, 6)), 1);
            return modulus;
        }

        private static byte[][] EdgeOperands()
        {
            return new[]
            {
                new byte[0], new byte[] { 0 }, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 0xff },
                new byte[8], new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, Filled(8, 0xff), Filled(9, 0xff), Filled(16, 0xff), Filled(33, 0xff),
                new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0, 0 }
            };
        }

        private static byte[][] EdgeModuli()
        {
            return new[]
            {
                new byte[] { 1 }, new byte[] { 0, 1 }, new byte[] { 2 }, new byte[] { 3 }, new byte[] { 0xff }, new byte[] { 0x80 },
                new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 }, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 1 }, new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0 },
                Filled(8, 0xff), Filled(16, 0xff), Filled(32, 0xff), Filled(40, 0xff), new byte[17] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6 }
            };
        }

        private static byte[] Filled(int length, byte value)
        {
            var bytes = new byte[length];
            Array.Fill(bytes, value);
            return bytes;
        }

        private static void Compare(byte[] baseBytes, byte[] exponent, byte[] modulus, StringBuilder failures)
        {
            if (new BigInteger(modulus, isUnsigned: true, isBigEndian: true).IsZero) return;
            var expected = Oracle(baseBytes, exponent, modulus);
            var actual = Backend.ModExp(baseBytes, exponent, modulus);
            if (!expected.AsSpan().SequenceEqual(actual))
                failures.AppendLine($"b={Convert.ToHexString(baseBytes)} e={Convert.ToHexString(exponent)} m={Convert.ToHexString(modulus)} expected={Convert.ToHexString(expected)} actual={Convert.ToHexString(actual)}");
        }

        private static byte[] Oracle(byte[] baseBytes, byte[] exponent, byte[] modulus)
        {
            var result = BigInteger.ModPow(Value(baseBytes), Value(exponent), Value(modulus));
            var bytes = result.ToByteArray(isUnsigned: true, isBigEndian: true);
            var padded = new byte[modulus.Length];
            Array.Copy(bytes, 0, padded, padded.Length - bytes.Length, bytes.Length);
            return padded;
        }

        private static BigInteger Value(byte[] bytes) => new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }
}
