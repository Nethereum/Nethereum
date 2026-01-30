using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using Nethereum.Signer.Crypto.BN128;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class BN128Fp2Tests
    {
        [Fact]
        public void Fp2_SetOne_ShouldBeCorrect()
        {
            var fp2 = new Fp2();
            fp2.SetOne();
            Assert.True(fp2.IsOne());
            Assert.Equal(BigInteger.Zero, fp2.A);
            Assert.Equal(BigInteger.One, fp2.B);
        }

        [Fact]
        public void Fp2_SetZero_ShouldBeCorrect()
        {
            var fp2 = new Fp2(BigInteger.One, BigInteger.Two);
            fp2.SetZero();
            Assert.True(fp2.IsZero());
        }

        [Fact]
        public void Fp2_Add_ShouldAddComponentWise()
        {
            var a = new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(5));
            var b = new Fp2(BigInteger.ValueOf(7), BigInteger.ValueOf(11));
            var result = new Fp2().Add(a, b);
            Assert.Equal(BigInteger.ValueOf(10), result.A);
            Assert.Equal(BigInteger.ValueOf(16), result.B);
        }

        [Fact]
        public void Fp2_Sub_ShouldSubtractComponentWise()
        {
            var a = new Fp2(BigInteger.ValueOf(10), BigInteger.ValueOf(20));
            var b = new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(5));
            var result = new Fp2().Sub(a, b);
            Assert.Equal(BigInteger.ValueOf(7), result.A);
            Assert.Equal(BigInteger.ValueOf(15), result.B);
        }

        [Fact]
        public void Fp2_Mul_ShouldUseComplexMultiplication()
        {
            var a = new Fp2(BigInteger.ValueOf(2), BigInteger.ValueOf(3));
            var b = new Fp2(BigInteger.ValueOf(4), BigInteger.ValueOf(5));
            var result = new Fp2().Mul(a, b);
            Assert.Equal(BigInteger.ValueOf(22), result.A);
            Assert.Equal(BigInteger.ValueOf(7), result.B);
        }

        [Fact]
        public void Fp2_Square_ShouldEqualMulSelf()
        {
            var a = new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4));
            var squared = new Fp2().Square(a);
            var mulled = new Fp2().Mul(a, a);
            Assert.Equal(mulled.A, squared.A);
            Assert.Equal(mulled.B, squared.B);
        }

        [Fact]
        public void Fp2_Invert_ShouldSatisfyInverseProperty()
        {
            var a = new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4));
            var inv = new Fp2().Invert(a);
            var product = new Fp2().Mul(a, inv);
            Assert.True(product.IsOne());
        }

        [Fact]
        public void Fp2_MulXi_ShouldMultiplyByNinePlusI()
        {
            var a = new Fp2(BigInteger.Zero, BigInteger.One);
            var result = new Fp2().MulXi(a);
            Assert.Equal(BigInteger.One, result.A);
            Assert.Equal(BigInteger.ValueOf(9), result.B);
        }

        [Fact]
        public void Fp2_Conjugate_ShouldNegateImaginaryPart()
        {
            var a = new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(7));
            var conj = new Fp2().Conjugate(a);
            Assert.Equal(a.A.Negate().Mod(Fp2.P), conj.A);
            Assert.Equal(a.B, conj.B);
        }
    }

    public class BN128Fp6Tests
    {
        [Fact]
        public void Fp6_SetOne_ShouldBeCorrect()
        {
            var fp6 = new Fp6();
            fp6.SetOne();
            Assert.True(fp6.IsOne());
            Assert.True(fp6.X.IsZero());
            Assert.True(fp6.Y.IsZero());
            Assert.True(fp6.Z.IsOne());
        }

        [Fact]
        public void Fp6_SetZero_ShouldBeCorrect()
        {
            var fp6 = new Fp6();
            fp6.SetOne();
            fp6.SetZero();
            Assert.True(fp6.IsZero());
        }

        [Fact]
        public void Fp6_Add_ShouldAddComponentWise()
        {
            var a = new Fp6(
                new Fp2(BigInteger.One, BigInteger.Two),
                new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6)));
            var b = new Fp6(
                new Fp2(BigInteger.ValueOf(10), BigInteger.ValueOf(20)),
                new Fp2(BigInteger.ValueOf(30), BigInteger.ValueOf(40)),
                new Fp2(BigInteger.ValueOf(50), BigInteger.ValueOf(60)));
            var result = new Fp6().Add(a, b);
            Assert.Equal(BigInteger.ValueOf(11), result.X.A);
            Assert.Equal(BigInteger.ValueOf(22), result.X.B);
        }

        [Fact]
        public void Fp6_Mul_ShouldSatisfyIdentityProperty()
        {
            var one = new Fp6();
            one.SetOne();
            var a = new Fp6(
                new Fp2(BigInteger.One, BigInteger.Two),
                new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6)));
            var result = new Fp6().Mul(a, one);
            Assert.Equal(a.X.A, result.X.A);
            Assert.Equal(a.X.B, result.X.B);
            Assert.Equal(a.Y.A, result.Y.A);
            Assert.Equal(a.Y.B, result.Y.B);
            Assert.Equal(a.Z.A, result.Z.A);
            Assert.Equal(a.Z.B, result.Z.B);
        }

        [Fact]
        public void Fp6_Square_ShouldEqualMulSelf()
        {
            var a = new Fp6(
                new Fp2(BigInteger.One, BigInteger.Two),
                new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6)));
            var squared = new Fp6().Square(a);
            var mulled = new Fp6().Mul(a, a);
            Assert.True(squared.Equals(mulled));
        }
    }

    public class BN128Fp12Tests
    {
        [Fact]
        public void Fp12_SetOne_ShouldBeCorrect()
        {
            var fp12 = new Fp12();
            fp12.SetOne();
            Assert.True(fp12.IsOne());
            Assert.True(fp12.X.IsZero());
            Assert.True(fp12.Y.IsOne());
        }

        [Fact]
        public void Fp12_Mul_ByOne_ShouldReturnSame()
        {
            var one = new Fp12();
            one.SetOne();
            var a = new Fp12(
                new Fp6(
                    new Fp2(BigInteger.One, BigInteger.Two),
                    new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                    new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6))),
                new Fp6(
                    new Fp2(BigInteger.ValueOf(7), BigInteger.ValueOf(8)),
                    new Fp2(BigInteger.ValueOf(9), BigInteger.ValueOf(10)),
                    new Fp2(BigInteger.ValueOf(11), BigInteger.ValueOf(12))));
            var result = new Fp12().Mul(a, one);
            Assert.True(a.Equals(result));
        }

        [Fact]
        public void Fp12_Square_ShouldEqualMulSelf()
        {
            var a = new Fp12(
                new Fp6(
                    new Fp2(BigInteger.One, BigInteger.Two),
                    new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                    new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6))),
                new Fp6(
                    new Fp2(BigInteger.ValueOf(7), BigInteger.ValueOf(8)),
                    new Fp2(BigInteger.ValueOf(9), BigInteger.ValueOf(10)),
                    new Fp2(BigInteger.ValueOf(11), BigInteger.ValueOf(12))));
            var squared = new Fp12().Square(a);
            var mulled = new Fp12().Mul(a, a);
            Assert.True(squared.Equals(mulled));
        }

        [Fact]
        public void Fp12_Invert_ShouldSatisfyInverseProperty()
        {
            var a = new Fp12(
                new Fp6(
                    new Fp2(BigInteger.One, BigInteger.Two),
                    new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                    new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6))),
                new Fp6(
                    new Fp2(BigInteger.ValueOf(7), BigInteger.ValueOf(8)),
                    new Fp2(BigInteger.ValueOf(9), BigInteger.ValueOf(10)),
                    new Fp2(BigInteger.ValueOf(11), BigInteger.ValueOf(12))));
            var inv = new Fp12().Invert(a);
            var product = new Fp12().Mul(a, inv);
            Assert.True(product.IsOne());
        }

        [Fact]
        public void Fp12_Conjugate_ShouldNegateX()
        {
            var a = new Fp12(
                new Fp6(
                    new Fp2(BigInteger.One, BigInteger.Two),
                    new Fp2(BigInteger.ValueOf(3), BigInteger.ValueOf(4)),
                    new Fp2(BigInteger.ValueOf(5), BigInteger.ValueOf(6))),
                new Fp6(
                    new Fp2(BigInteger.ValueOf(7), BigInteger.ValueOf(8)),
                    new Fp2(BigInteger.ValueOf(9), BigInteger.ValueOf(10)),
                    new Fp2(BigInteger.ValueOf(11), BigInteger.ValueOf(12))));
            var conj = new Fp12().Conjugate(a);
            Assert.True(a.Y.Equals(conj.Y));
        }
    }

    public class BN128TwistPointTests
    {
        [Fact]
        public void TwistPoint_Infinity_ShouldBeInfinity()
        {
            var p = new TwistPoint();
            p.SetInfinity();
            Assert.True(p.IsInfinity());
        }

        [Fact]
        public void TwistPoint_Double_ShouldWork()
        {
            var g2 = GetG2Generator();
            var doubled = new TwistPoint().Double(g2);
            Assert.False(doubled.IsInfinity());
        }

        [Fact]
        public void TwistPoint_Add_InfinityPlusPoint_ShouldReturnPoint()
        {
            var infinity = new TwistPoint();
            infinity.SetInfinity();
            var g2 = GetG2Generator();
            var result = new TwistPoint().Add(infinity, g2);
            result.MakeAffine();
            var g2Copy = GetG2Generator();
            g2Copy.MakeAffine();
            Assert.Equal(g2Copy.X.A, result.X.A);
            Assert.Equal(g2Copy.X.B, result.X.B);
            Assert.Equal(g2Copy.Y.A, result.Y.A);
            Assert.Equal(g2Copy.Y.B, result.Y.B);
        }

        [Fact]
        public void TwistPoint_Add_PointPlusNegative_ShouldReturnInfinity()
        {
            var g2 = GetG2Generator();
            var negG2 = new TwistPoint().Neg(g2);
            var result = new TwistPoint().Add(g2, negG2);
            Assert.True(result.IsInfinity());
        }

        private TwistPoint GetG2Generator()
        {
            var g2x = new Fp2(
                new BigInteger("11559732032986387107991004021392285783925812861821192530917403151452391805634"),
                new BigInteger("10857046999023057135944570762232829481370756359578518086990519993285655852781"));
            var g2y = new Fp2(
                new BigInteger("4082367875863433681332203403145435568316851327593401208105741076214120093531"),
                new BigInteger("8495653923123431417604973247489272438418190587263600148770280649306958101930"));
            return TwistPoint.FromAffine(g2x, g2y);
        }
    }

    public class BN128PairingTests
    {
        public static IEnumerable<object[]> PairingEmptyInputTestCase()
        {
            yield return new object[] { "empty_data", "", "0000000000000000000000000000000000000000000000000000000000000001" };
        }

        public static IEnumerable<object[]> PairingSinglePointTestCases()
        {
            yield return new object[] { "one_point",
                "00000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000002198e9393920d483a7260bfb731fb5d25f1aa493335a9e71297e485b7aef312c21800deef121f1e76426a00665e5c4479674322d4f75edadd46debd5cd992f6ed090689d0585ff075ec9e99ad690c3395bc4b313370b38ef355acdadcd122975b12c85ea5db8c6deb4aab71808dcb408fe3d1e7690c43d37b4ce6cc0166fa7daa",
                "0000000000000000000000000000000000000000000000000000000000000000" };
        }

        public static IEnumerable<object[]> PairingTwoPointTestCases()
        {
            yield return new object[] { "two_point_match_2",
                "00000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000002198e9393920d483a7260bfb731fb5d25f1aa493335a9e71297e485b7aef312c21800deef121f1e76426a00665e5c4479674322d4f75edadd46debd5cd992f6ed090689d0585ff075ec9e99ad690c3395bc4b313370b38ef355acdadcd122975b12c85ea5db8c6deb4aab71808dcb408fe3d1e7690c43d37b4ce6cc0166fa7daa00000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000002198e9393920d483a7260bfb731fb5d25f1aa493335a9e71297e485b7aef312c21800deef121f1e76426a00665e5c4479674322d4f75edadd46debd5cd992f6ed275dc4a288d1afb3cbb1ac09187524c7db36395df7be3b99e673b13a075a65ec1d9befcd05a5323e6da4d435f3b617cdb3af83285c2df711ef39c01571827f9d",
                "0000000000000000000000000000000000000000000000000000000000000001" };
        }

        public static IEnumerable<object[]> PairingInvalidLengthTestCases()
        {
            yield return new object[] { "invalid_length_100bytes", new string('0', 200) };
            yield return new object[] { "invalid_length_191bytes", new string('0', 382) };
            yield return new object[] { "invalid_length_193bytes", new string('0', 386) };
        }

        [Theory]
        [MemberData(nameof(PairingEmptyInputTestCase))]
        public void Pairing_EmptyInput_ShouldReturnOne(string name, string input, string expectedOutput)
        {
            var inputBytes = string.IsNullOrEmpty(input) ? new byte[0] : input.HexToByteArray();
            var result = BN128Curve.Pairing(inputBytes);
            Assert.Equal(expectedOutput.ToLower(), result.ToHex().ToLower());
        }

        [Theory]
        [MemberData(nameof(PairingSinglePointTestCases))]
        public void Pairing_SinglePoint_ShouldReturnZero(string name, string input, string expectedOutput)
        {
            var inputBytes = input.HexToByteArray();
            var result = BN128Curve.Pairing(inputBytes);
            Assert.Equal(expectedOutput.ToLower(), result.ToHex().ToLower());
        }

        [Theory]
        [MemberData(nameof(PairingTwoPointTestCases))]
        public void Pairing_TwoPoints_ShouldReturnOne(string name, string input, string expectedOutput)
        {
            var inputBytes = input.HexToByteArray();
            var result = BN128Curve.Pairing(inputBytes);
            Assert.Equal(expectedOutput.ToLower(), result.ToHex().ToLower());
        }

        [Theory]
        [MemberData(nameof(PairingInvalidLengthTestCases))]
        public void Pairing_InvalidLength_ShouldThrow(string name, string input)
        {
            var inputBytes = input.HexToByteArray();
            Assert.Throws<ArgumentException>(() => BN128Curve.Pairing(inputBytes));
        }

        [Fact]
        public void Pairing_G1Generator_G2Generator_ShouldNotBeOne()
        {
            var g1 = BN128Curve.Curve.CreatePoint(BigInteger.One, BigInteger.Two);
            var g2x = new Fp2(
                new BigInteger("11559732032986387107991004021392285783925812861821192530917403151452391805634"),
                new BigInteger("10857046999023057135944570762232829481370756359578518086990519993285655852781"));
            var g2y = new Fp2(
                new BigInteger("4082367875863433681332203403145435568316851327593401208105741076214120093531"),
                new BigInteger("8495653923123431417604973247489272438418190587263600148770280649306958101930"));
            var g2 = TwistPoint.FromAffine(g2x, g2y);

            var pairingResult = BN128Pairing.Pair(g1, g2);
            Assert.False(pairingResult.IsOne());
        }

        [Fact]
        public void Pairing_InfinityG1_ShouldReturnOne()
        {
            var infinityG1 = BN128Curve.Curve.Infinity;
            var g2x = new Fp2(
                new BigInteger("11559732032986387107991004021392285783925812861821192530917403151452391805634"),
                new BigInteger("10857046999023057135944570762232829481370756359578518086990519993285655852781"));
            var g2y = new Fp2(
                new BigInteger("4082367875863433681332203403145435568316851327593401208105741076214120093531"),
                new BigInteger("8495653923123431417604973247489272438418190587263600148770280649306958101930"));
            var g2 = TwistPoint.FromAffine(g2x, g2y);

            var result = BN128Pairing.Pair(infinityG1, g2);
            Assert.True(result.IsOne());
        }

        [Fact]
        public void Pairing_InfinityG2_ShouldReturnOne()
        {
            var g1 = BN128Curve.Curve.CreatePoint(BigInteger.One, BigInteger.Two);
            var infinityG2 = new TwistPoint();
            infinityG2.SetInfinity();

            var result = BN128Pairing.Pair(g1, infinityG2);
            Assert.True(result.IsOne());
        }

        [Fact]
        public void PairingCheck_EmptyArrays_ShouldReturnTrue()
        {
            var result = BN128Pairing.PairingCheck(new ECPoint[0], new TwistPoint[0]);
            Assert.True(result);
        }

        [Theory]
        [MemberData(nameof(GethPairingTestCases))]
        public void Pairing_GethTestVectors(string name, string input, string expectedOutput)
        {
            var inputBytes = string.IsNullOrEmpty(input) ? new byte[0] : input.HexToByteArray();
            var result = BN128Curve.Pairing(inputBytes);
            Assert.Equal(expectedOutput.ToLower(), result.ToHex().ToLower());
        }

        public static IEnumerable<object[]> GethPairingTestCases()
        {
            return BN128GethTestVectors.PairingTestCases();
        }

        [Fact]
        public void Pairing_Bilinearity_ScalarOnG1()
        {
            var g1 = BN128Curve.Curve.CreatePoint(BigInteger.One, BigInteger.Two);
            var g2x = new Fp2(
                new BigInteger("11559732032986387107991004021392285783925812861821192530917403151452391805634"),
                new BigInteger("10857046999023057135944570762232829481370756359578518086990519993285655852781"));
            var g2y = new Fp2(
                new BigInteger("4082367875863433681332203403145435568316851327593401208105741076214120093531"),
                new BigInteger("8495653923123431417604973247489272438418190587263600148770280649306958101930"));
            var g2 = TwistPoint.FromAffine(g2x, g2y);

            var two = BigInteger.Two;
            var twoG1 = g1.Multiply(two);

            var e1 = BN128Pairing.Pair(g1, g2);
            var e2 = BN128Pairing.Pair(twoG1, g2);

            var e1Squared = new Fp12().Square(e1);
            Assert.True(e1Squared.Equals(e2));
        }
    }
}
