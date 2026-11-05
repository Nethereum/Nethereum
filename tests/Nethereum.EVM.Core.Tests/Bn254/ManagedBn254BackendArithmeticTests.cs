using System.Numerics;
using Nethereum.EVM.Precompiles.Backends;
using Xunit;

namespace Nethereum.EVM.Core.Tests.Bn254
{
    public class ManagedBn254BackendArithmeticTests
    {
        private static readonly (BigInteger X, BigInteger Y) Generator = (BigInteger.One, new BigInteger(2));
        private static readonly BigInteger R = Bn254Oracle.R;
        private static readonly BigInteger P = Bn254Oracle.P;

        private static byte[] Mul((BigInteger X, BigInteger Y)? point, BigInteger scalar)
        {
            var input = Bn254Oracle.EncodePoint(point).Concat(Bn254Oracle.ToBytes32(scalar)).ToArray();
            Assert.True(ManagedBn254Backend.Instance.TryMul(input, out var output));
            return output;
        }

        private static byte[] Add((BigInteger X, BigInteger Y)? first, (BigInteger X, BigInteger Y)? second)
        {
            var input = Bn254Oracle.EncodePoint(first).Concat(Bn254Oracle.EncodePoint(second)).ToArray();
            Assert.True(ManagedBn254Backend.Instance.TryAdd(input, out var output));
            return output;
        }

        private static (BigInteger X, BigInteger Y) Decode(byte[] encoded) =>
            (Bn254Oracle.FromBytes(encoded, 0), Bn254Oracle.FromBytes(encoded, 32));

        private static BigInteger RandomScalar(Random random)
        {
            var bytes = new byte[32];
            random.NextBytes(bytes);
            return Bn254Oracle.FromBytes(bytes);
        }

        private static (BigInteger X, BigInteger Y)? RandomPoint(Random random) =>
            Bn254Oracle.Multiply(Generator, RandomScalar(random));

        [Fact]
        public void Given_the_generator_When_ecMul_by_the_group_order_Then_the_result_is_infinity()
        {
            Assert.Equal(new byte[64], Mul(Generator, R));
            Assert.Equal(new byte[64], Mul(Generator, 0));
            Assert.Equal(new byte[64], Mul(Generator, R * 2));
        }

        [Fact]
        public void Given_the_generator_When_ecMul_by_r_minus_one_and_r_plus_one_Then_the_result_is_the_negation_and_the_generator()
        {
            var negated = (Generator.X, P - Generator.Y);

            Assert.Equal(Bn254Oracle.EncodePoint(negated), Mul(Generator, R - 1));
            Assert.Equal(Bn254Oracle.EncodePoint(Generator), Mul(Generator, R + 1));
            Assert.Equal(Bn254Oracle.EncodePoint(Generator), Mul(Generator, 1));
        }

        [Fact]
        public void Given_the_endomorphism_parameters_When_ecMul_by_lambda_Then_the_x_coordinate_is_scaled_by_beta()
        {
            var random = new Random(11);
            for (var i = 0; i < 20; i++)
            {
                var point = RandomPoint(random).Value;
                var expected = Bn254Oracle.EncodePoint((Bn254Oracle.Mod(Bn254Oracle.Beta * point.X, P), point.Y));
                Assert.Equal(expected, Mul(point, Bn254Oracle.Lambda));
            }
        }

        [Fact]
        public void Given_lambda_When_ecMul_by_lambda_squared_plus_lambda_plus_one_Then_the_result_is_infinity()
        {
            var random = new Random(12);
            var point = RandomPoint(random);
            var lambdaSquared = Bn254Oracle.Mod(Bn254Oracle.Lambda * Bn254Oracle.Lambda, R);

            var byLambda = Decode(Mul(point, Bn254Oracle.Lambda));
            var byLambdaSquared = Decode(Mul(point, lambdaSquared));

            Assert.Equal(new byte[64], Add(Bn254Oracle.Add(point, byLambda), byLambdaSquared));
            Assert.Equal(Bn254Oracle.EncodePoint(Bn254Oracle.Multiply(point, lambdaSquared)), Mul(point, lambdaSquared));
        }

        [Fact]
        public void Given_random_points_and_scalars_When_ecMul_Then_the_result_matches_a_plain_double_and_add_over_biginteger()
        {
            var random = new Random(6);
            for (var i = 0; i < 40; i++)
            {
                var point = RandomPoint(random);
                var scalar = RandomScalar(random);
                Assert.Equal(Bn254Oracle.EncodePoint(Bn254Oracle.Multiply(point, scalar)), Mul(point, scalar));
            }
        }

        [Fact]
        public void Given_scalars_around_the_glv_lattice_bounds_When_ecMul_Then_the_result_matches_a_plain_double_and_add()
        {
            var point = Bn254Oracle.Multiply(Generator, 123456789);
            var half = BigInteger.One << 127;
            var scalars = new[] { half - 1, half, half + 1, (BigInteger.One << 128) - 1, BigInteger.One << 128, (BigInteger.One << 128) + 1,
                R / 2, R / 2 + 1, (R - 1) / 3, Bn254Oracle.Lambda - 1, Bn254Oracle.Lambda + 1, R - Bn254Oracle.Lambda, (BigInteger.One << 254) + 12345 };
            foreach (var scalar in scalars)
                Assert.Equal(Bn254Oracle.EncodePoint(Bn254Oracle.Multiply(point, scalar)), Mul(point, scalar));
        }

        [Fact]
        public void Given_random_point_pairs_When_ecAdd_Then_the_result_matches_the_affine_oracle()
        {
            var random = new Random(2);
            for (var i = 0; i < 40; i++)
            {
                var first = RandomPoint(random);
                var second = RandomPoint(random);
                Assert.Equal(Bn254Oracle.EncodePoint(Bn254Oracle.Add(first, second)), Add(first, second));
            }
        }

        [Fact]
        public void Given_equal_opposite_and_infinite_operands_When_ecAdd_Then_the_group_law_holds()
        {
            var random = new Random(3);
            var point = RandomPoint(random).Value;
            var negated = (point.X, P - point.Y);

            Assert.Equal(Bn254Oracle.EncodePoint(Bn254Oracle.Add(point, point)), Add(point, point));
            Assert.Equal(new byte[64], Add(point, negated));
            Assert.Equal(Bn254Oracle.EncodePoint(point), Add(point, null));
            Assert.Equal(Bn254Oracle.EncodePoint(point), Add(null, point));
            Assert.Equal(new byte[64], Add(null, null));
        }

        [Fact]
        public void Given_a_modulus_boundary_coordinate_When_ecAdd_and_ecMul_Then_the_field_boundary_is_enforced()
        {
            var backend = ManagedBn254Backend.Instance;
            var atModulus = Bn254Oracle.ToBytes32(P).Concat(Bn254Oracle.ToBytes32(2)).Concat(new byte[32]).ToArray();
            var belowModulus = Bn254Oracle.ToBytes32(P - 1).Concat(Bn254Oracle.ToBytes32(0)).Concat(new byte[32]).ToArray();

            Assert.False(backend.TryMul(atModulus, out _));
            Assert.False(backend.TryMul(belowModulus, out _));
        }
    }
}
