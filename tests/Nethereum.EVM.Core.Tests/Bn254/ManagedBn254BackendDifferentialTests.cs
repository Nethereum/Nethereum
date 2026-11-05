using System.Numerics;
using System.Text;
using Nethereum.EVM.Precompiles.Backends;
using Xunit;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    public class ManagedBn254BackendDifferentialTests
    {
        private const int AlwaysOnCases = 2000;
        private const int FullCases = 100000;
        private const int Seed = 20261020;

        private static readonly BigInteger P = Bn254Oracle.P;
        private static readonly BigInteger R = Bn254Oracle.R;
        private static readonly BigInteger Max = (BigInteger.One << 256) - 1;

        private static readonly ManagedBn254Backend Managed = ManagedBn254Backend.Instance;
        private static readonly TextbookAffineReference Reference = new TextbookAffineReference();

        [Fact]
        public void Given_random_points_and_scalars_When_the_managed_backend_and_the_textbook_reference_execute_Then_outputs_are_byte_identical()
        {
            AssertIdentical(AlwaysOnCases);
        }

        [Fact]
        [Trait("Category", "Bn254Differential")]
        [Trait("Category", "LargeFixture")]
        public void Given_one_hundred_thousand_random_points_and_scalars_When_the_managed_backend_and_the_textbook_reference_execute_Then_outputs_are_byte_identical()
        {
            AssertIdentical(FullCases);
        }

        [Fact]
        public void Given_the_edge_points_and_scalars_When_ecMul_Then_the_managed_backend_and_the_textbook_reference_agree_including_failures()
        {
            var failures = new StringBuilder();
            foreach (var point in EdgePoints())
                foreach (var scalar in EdgeScalars())
                    CompareMul(Concat(point, Bn254Oracle.ToBytes32(scalar)), failures);

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_the_edge_point_pairs_When_ecAdd_Then_the_managed_backend_and_the_textbook_reference_agree_including_failures()
        {
            var failures = new StringBuilder();
            foreach (var first in EdgePoints())
                foreach (var second in EdgePoints())
                    CompareAdd(Concat(first, second), failures);

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_an_off_curve_or_out_of_field_point_When_ecMul_Then_it_fails_exactly_as_the_reference()
        {
            var failures = new StringBuilder();
            var invalid = new[]
            {
                Point(1, 1), Point(P, 2), Point(1, P), Point(P + 1, 2), Point(P - 1, P - 1), Point(Max, Max), Point(0, 1), Point(0, P - 1)
            };
            foreach (var point in invalid)
                foreach (var scalar in EdgeScalars())
                {
                    var input = Concat(point, Bn254Oracle.ToBytes32(scalar));
                    CompareMul(input, failures);
                    Assert.False(Managed.TryMul(input, out _));
                }

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_short_and_long_inputs_When_ecAdd_and_ecMul_Then_the_managed_backend_and_the_textbook_reference_agree()
        {
            var failures = new StringBuilder();
            var random = new Random(Seed);
            var full = Concat(Point(1, 2), Bn254Oracle.ToBytes32(R - 1), Bn254Oracle.ToBytes32(12345));
            for (var length = 0; length <= full.Length + 40; length++)
            {
                var input = new byte[length];
                Array.Copy(full, input, Math.Min(length, full.Length));
                for (var i = full.Length; i < length; i++) input[i] = (byte)random.Next(256);
                CompareMul(input, failures);
                CompareAdd(input, failures);
            }
            CompareMul(null, failures);
            CompareAdd(null, failures);

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_points_with_a_zero_y_coordinate_When_ecAdd_and_ecMul_Then_the_managed_backend_and_the_textbook_reference_agree()
        {
            var failures = new StringBuilder();
            var random = new Random(Seed);
            for (var i = 0; i < 200; i++)
            {
                var x = RandomBelow(random, P);
                var point = Point(x, 0);
                CompareMul(Concat(point, Bn254Oracle.ToBytes32(RandomScalar(random))), failures);
                CompareAdd(Concat(point, Point(1, 2)), failures);
                CompareAdd(Concat(point, point), failures);
            }

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void Given_a_point_and_its_negation_When_ecAdd_Then_the_managed_backend_and_the_textbook_reference_return_infinity()
        {
            var failures = new StringBuilder();
            var random = new Random(Seed);
            for (var i = 0; i < 100; i++)
            {
                var point = RandomValidPoint(random);
                CompareAdd(Concat(Point(point.X, point.Y), Point(point.X, P - point.Y)), failures);
                CompareAdd(Concat(Point(point.X, point.Y), Point(point.X, point.Y)), failures);
            }

            Assert.Equal(string.Empty, failures.ToString());
        }

        private static void AssertIdentical(int cases)
        {
            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, cases, index =>
            {
                var random = new Random(Seed ^ (index * 7919 + 1));
                var local = new StringBuilder();
                CompareMul(RandomMulInput(random), local);
                CompareAdd(RandomAddInput(random), local);
                if (local.Length > 0) failures.Add(local.ToString());
            });

            Assert.True(failures.IsEmpty, string.Join(Environment.NewLine, failures.Take(5)));
        }

        private static void CompareMul(byte[] input, StringBuilder failures) =>
            Compare("mul", input, Reference.TryMul(input, out var expected), expected, Managed.TryMul(input, out var actual), actual, failures);

        private static void CompareAdd(byte[] input, StringBuilder failures) =>
            Compare("add", input, Reference.TryAdd(input, out var expected), expected, Managed.TryAdd(input, out var actual), actual, failures);

        private static void Compare(string operation, byte[] input, bool expectedOk, byte[] expected, bool actualOk, byte[] actual, StringBuilder failures)
        {
            if (expectedOk == actualOk && (!expectedOk || expected.AsSpan().SequenceEqual(actual))) return;
            var inputHex = input == null ? "null" : Convert.ToHexString(input);
            failures.AppendLine($"{operation} input={inputHex} reference={expectedOk}:{(expected == null ? "" : Convert.ToHexString(expected))} managed={actualOk}:{(actual == null ? "" : Convert.ToHexString(actual))}");
        }

        private static byte[] RandomMulInput(Random random)
        {
            var input = Concat(RandomPoint(random), Bn254Oracle.ToBytes32(RandomScalar(random)));
            return RandomLength(random, input);
        }

        private static byte[] RandomAddInput(Random random)
        {
            var first = RandomPoint(random);
            var second = random.Next(8) switch
            {
                0 => first,
                1 => Negated(first),
                _ => RandomPoint(random)
            };
            return RandomLength(random, Concat(first, second));
        }

        private static byte[] RandomLength(Random random, byte[] input)
        {
            switch (random.Next(20))
            {
                case 0: return input.AsSpan(0, random.Next(input.Length + 1)).ToArray();
                case 1: return Concat(input, RandomBytes(random, random.Next(1, 40)));
                default: return input;
            }
        }

        private static byte[] RandomPoint(Random random)
        {
            switch (random.Next(20))
            {
                case 0: return Point(0, 0);
                case 1: return Point(1, 2);
                case 2: return Point(RandomBelow(random, P), RandomBelow(random, P));
                case 3: return Point(P + random.Next(3), RandomBelow(random, P));
                case 4: return Point(RandomBelow(random, P), P + random.Next(3));
                case 5: return Point(RandomBelow(random, P), 0);
                case 6:
                    var offCurve = RandomValidPoint(random);
                    return Point(offCurve.X, Bn254Oracle.Mod(offCurve.Y + 1, P));
                default:
                    var valid = RandomValidPoint(random);
                    return Point(valid.X, valid.Y);
            }
        }

        private static (BigInteger X, BigInteger Y) RandomValidPoint(Random random)
        {
            while (true)
            {
                var x = RandomBelow(random, P);
                var y = Bn254Oracle.SquareRoot(x * x * x + 3);
                if (y == null) continue;
                return (x, random.Next(2) == 0 ? y.Value : Bn254Oracle.Mod(-y.Value, P));
            }
        }

        private static BigInteger RandomScalar(Random random)
        {
            switch (random.Next(12))
            {
                case 0: return random.Next(0, 1000);
                case 1: return R - 1 + random.Next(-3, 4);
                case 2: return Max - random.Next(4);
                case 3: return BigInteger.One << random.Next(0, 256);
                case 4: return (BigInteger.One << random.Next(1, 257)) - 1;
                case 5: return R * random.Next(1, 6) + random.Next(-2, 3);
                case 6: return Bn254Oracle.FromBytes(RandomBytes(random, 32)) >> random.Next(0, 256);
                case 7: return Bn254Oracle.Lambda + random.Next(-2, 3);
                default: return Bn254Oracle.FromBytes(RandomBytes(random, 32));
            }
        }

        private static IEnumerable<byte[]> EdgePoints()
        {
            var random = new Random(Seed);
            var valid = RandomValidPoint(random);
            yield return Point(0, 0);
            yield return Point(1, 2);
            yield return Point(1, P - 2);
            yield return Point(valid.X, valid.Y);
            yield return Point(valid.X, P - valid.Y);
            yield return Point(P - 1, P - 1);
            yield return Point(P - 1, 0);
            yield return Point(P, 2);
            yield return Point(1, P);
            yield return Point(P + 1, 2);
            yield return Point(1, P + 1);
            yield return Point(0, 1);
            yield return Point(1, 1);
            yield return Point(2, 0);
            yield return Point(Max, Max);
        }

        private static IEnumerable<BigInteger> EdgeScalars()
        {
            yield return 0;
            yield return 1;
            yield return 2;
            yield return R - 1;
            yield return R;
            yield return R + 1;
            yield return 2 * R;
            yield return Max;
            yield return Max - R;
            yield return P;
            yield return Bn254Oracle.Lambda;
        }

        private static byte[] Point(BigInteger x, BigInteger y) =>
            Concat(Bn254Oracle.ToBytes32(x), Bn254Oracle.ToBytes32(y));

        private static byte[] Negated(byte[] point)
        {
            var y = Bn254Oracle.FromBytes(point, 32);
            return Concat(point.AsSpan(0, 32).ToArray(), Bn254Oracle.ToBytes32(y.IsZero ? y : Bn254Oracle.Mod(-y, P)));
        }

        private static BigInteger RandomBelow(Random random, BigInteger bound) =>
            Bn254Oracle.Mod(Bn254Oracle.FromBytes(RandomBytes(random, 32)), bound);

        private static byte[] RandomBytes(Random random, int length)
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            return bytes;
        }

        private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        private sealed class TextbookAffineReference
        {
            public bool TryAdd(byte[] input, out byte[] output)
            {
                var data = Padded(input, 128);
                output = null;
                if (!TryReadPoint(data, 0, out var first)) return false;
                if (!TryReadPoint(data, 64, out var second)) return false;
                output = Bn254Oracle.EncodePoint(Bn254Oracle.Add(first, second));
                return true;
            }

            public bool TryMul(byte[] input, out byte[] output)
            {
                var data = Padded(input, 96);
                output = null;
                if (!TryReadPoint(data, 0, out var point)) return false;
                output = Bn254Oracle.EncodePoint(Bn254Oracle.Multiply(point, Bn254Oracle.FromBytes(data, 64)));
                return true;
            }

            private static bool TryReadPoint(byte[] data, int offset, out (BigInteger X, BigInteger Y)? point)
            {
                point = null;
                var x = Bn254Oracle.FromBytes(data, offset);
                var y = Bn254Oracle.FromBytes(data, offset + 32);
                if (x >= P || y >= P) return false;
                if (x.IsZero && y.IsZero) return true;
                if (!Bn254Oracle.IsOnCurve(x, y)) return false;
                point = (x, y);
                return true;
            }

            private static byte[] Padded(byte[] input, int length)
            {
                var padded = new byte[length];
                if (input != null) Array.Copy(input, padded, Math.Min(input.Length, length));
                return padded;
            }
        }
    }
}
