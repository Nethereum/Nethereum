using System;
using Nethereum.Signer.Crypto.BN128;
using Org.BouncyCastle.Math;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class FpMontgomeryDifferentialTests
    {
        private static readonly BigInteger P = BN128Constants.P;

        [Fact]
        public void Given_random_field_elements_When_add_sub_mul_sqr_neg_Then_match_BigInteger_mod_p()
        {
            var random = new Random(12345);
            for (var i = 0; i < 4000; i++)
            {
                var a = RandomModP(random);
                var b = RandomModP(random);
                var fa = Fp.FromBigInteger(a);
                var fb = Fp.FromBigInteger(b);

                Assert.Equal(a.Add(b).Mod(P), fa.Add(fb).ToBigInteger());
                Assert.Equal(a.Subtract(b).Mod(P), fa.Sub(fb).ToBigInteger());
                Assert.Equal(a.Multiply(b).Mod(P), fa.Mul(fb).ToBigInteger());
                Assert.Equal(a.Multiply(a).Mod(P), fa.Sqr().ToBigInteger());
                Assert.Equal(P.Subtract(a).Mod(P), fa.Neg().ToBigInteger());
            }
        }

        [Fact]
        public void Given_a_field_element_When_round_tripped_through_bytes_and_bigint_Then_unchanged()
        {
            var random = new Random(54321);
            for (var i = 0; i < 2000; i++)
            {
                var a = RandomModP(random);
                var fa = Fp.FromBigInteger(a);

                Assert.Equal(a, fa.ToBigInteger());
                Assert.Equal(a, Fp.FromBytesBigEndian(fa.ToBytes32BigEndian()).ToBigInteger());
            }
        }

        [Fact]
        public void Given_a_nonzero_element_When_inverted_Then_product_is_one_and_matches_modinverse()
        {
            var random = new Random(999);
            for (var i = 0; i < 100; i++)
            {
                var a = RandomModP(random);
                if (a.SignValue == 0) continue;
                var fa = Fp.FromBigInteger(a);
                var inverse = fa.Inv();

                Assert.Equal(BigInteger.One, fa.Mul(inverse).ToBigInteger());
                Assert.Equal(a.ModInverse(P), inverse.ToBigInteger());
            }
        }

        [Fact]
        public void Given_zero_and_one_When_read_Then_correct()
        {
            Assert.True(Fp.Zero.IsZero);
            Assert.Equal(BigInteger.Zero, Fp.Zero.ToBigInteger());
            Assert.Equal(BigInteger.One, Fp.One.ToBigInteger());
        }

        private static BigInteger RandomModP(Random random)
        {
            var bytes = new byte[32];
            random.NextBytes(bytes);
            return new BigInteger(1, bytes).Mod(P);
        }
    }
}
