using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256NarrowingConversionTests
    {
        private static EvmUInt256 U(ulong u3, ulong u2, ulong u1, ulong u0) => new EvmUInt256(u3, u2, u1, u0);

        private static void AssertExact<T>(string caseName, T expected, T actual) where T : IEquatable<T>
            => Assert.True(expected.Equals(actual), caseName + ": expected " + expected + " but conversion produced " + actual);

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, (byte)0)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, (byte)1)]
        [InlineData("byte.MaxValue", 0UL, 0UL, 0UL, 255UL, (byte)255)]
        [InlineData("byte.MaxValue+1 wraps to 0", 0UL, 0UL, 0UL, 256UL, (byte)0)]
        [InlineData("u0 only, exceeds byte", 0UL, 0UL, 0UL, 0x1234UL, (byte)0x34)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, (byte)42)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, (byte)42)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, (byte)255)]
        public void Given_Boundary_When_CastToByte_Then_LowestByteOfU0IsReturned(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, byte expected)
            => AssertExact(caseName, expected, (byte)U(u3, u2, u1, u0));

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, (ushort)0)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, (ushort)1)]
        [InlineData("ushort.MaxValue", 0UL, 0UL, 0UL, 65535UL, (ushort)65535)]
        [InlineData("ushort.MaxValue+1 wraps to 0", 0UL, 0UL, 0UL, 65536UL, (ushort)0)]
        [InlineData("u0 only, exceeds ushort", 0UL, 0UL, 0UL, 0x12345678UL, (ushort)0x5678)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, (ushort)42)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, (ushort)42)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, (ushort)65535)]
        public void Given_Boundary_When_CastToUShort_Then_LowestTwoBytesOfU0AreReturned(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, ushort expected)
            => AssertExact(caseName, expected, (ushort)U(u3, u2, u1, u0));

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0u)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, 1u)]
        [InlineData("uint.MaxValue", 0UL, 0UL, 0UL, 4294967295UL, 4294967295u)]
        [InlineData("uint.MaxValue+1 wraps to 0", 0UL, 0UL, 0UL, 4294967296UL, 0u)]
        [InlineData("u0 only, exceeds uint", 0UL, 0UL, 0UL, 0x123456789ABCDEF0UL, 0x9ABCDEF0u)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, 42u)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, 42u)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 4294967295u)]
        public void Given_Boundary_When_CastToUInt_Then_LowestFourBytesOfU0AreReturned(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, uint expected)
            => AssertExact(caseName, expected, (uint)U(u3, u2, u1, u0));

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, 1)]
        [InlineData("int.MaxValue", 0UL, 0UL, 0UL, 2147483647UL, 2147483647)]
        [InlineData("int.MaxValue+1 becomes int.MinValue", 0UL, 0UL, 0UL, 2147483648UL, int.MinValue)]
        [InlineData("2^32-1 becomes -1", 0UL, 0UL, 0UL, 4294967295UL, -1)]
        [InlineData("2^32 wraps to 0", 0UL, 0UL, 0UL, 4294967296UL, 0)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, 42)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, 42)]
        [InlineData("2^256-1 becomes -1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, -1)]
        public void Given_Boundary_When_CastToInt_Then_LowestFourBytesReinterpretedSigned(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, int expected)
            => AssertExact(caseName, expected, (int)U(u3, u2, u1, u0));

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0)]
        [InlineData("int.MaxValue", 0UL, 0UL, 0UL, 2147483647UL, 2147483647)]
        [InlineData("int.MaxValue+1 becomes int.MinValue", 0UL, 0UL, 0UL, 2147483648UL, int.MinValue)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, 42)]
        [InlineData("2^256-1 becomes -1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, -1)]
        public void Given_Boundary_When_ToInt_Then_MatchesTheExplicitIntCast(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, int expected)
            => AssertExact(caseName, expected, U(u3, u2, u1, u0).ToInt());

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0L)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, 1L)]
        [InlineData("long.MaxValue", 0UL, 0UL, 0UL, 9223372036854775807UL, long.MaxValue)]
        [InlineData("long.MaxValue+1 becomes long.MinValue", 0UL, 0UL, 0UL, 9223372036854775808UL, long.MinValue)]
        [InlineData("2^64-1 becomes -1", 0UL, 0UL, 0UL, ulong.MaxValue, -1L)]
        [InlineData("2^64 wraps to 0", 0UL, 0UL, 1UL, 0UL, 0L)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, 42L)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, 42L)]
        [InlineData("2^256-1 becomes -1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, -1L)]
        public void Given_Boundary_When_CastToLong_Then_U0ReinterpretedSigned(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, long expected)
            => AssertExact(caseName, expected, (long)U(u3, u2, u1, u0));

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0L)]
        [InlineData("long.MaxValue", 0UL, 0UL, 0UL, 9223372036854775807UL, long.MaxValue)]
        [InlineData("long.MaxValue+1 becomes long.MinValue", 0UL, 0UL, 0UL, 9223372036854775808UL, long.MinValue)]
        [InlineData("u1 set, u0 looks innocent", 0UL, 0UL, 1UL, 42UL, 42L)]
        [InlineData("2^256-1 becomes -1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, -1L)]
        public void Given_Boundary_When_ToLong_Then_MatchesTheExplicitLongCast(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, long expected)
            => AssertExact(caseName, expected, U(u3, u2, u1, u0).ToLong());

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0UL)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, 1UL)]
        [InlineData("ulong.MaxValue", 0UL, 0UL, 0UL, ulong.MaxValue, ulong.MaxValue)]
        [InlineData("ulong.MaxValue+1 (2^64) becomes 0", 0UL, 0UL, 1UL, 0UL, 0UL)]
        [InlineData("2^64+42 becomes 42", 0UL, 0UL, 1UL, 42UL, 42UL)]
        [InlineData("u2 set, u0 zero, becomes 0", 0UL, 1UL, 0UL, 0UL, 0UL)]
        [InlineData("u3 set, u0 zero, becomes 0", 1UL, 0UL, 0UL, 0UL, 0UL)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, 42UL)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue)]
        public void Given_Boundary_When_CastToULong_Then_U0IsReturnedUnchecked(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, ulong expected)
            => AssertExact(caseName, expected, (ulong)U(u3, u2, u1, u0));

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0UL)]
        [InlineData("ulong.MaxValue", 0UL, 0UL, 0UL, ulong.MaxValue, ulong.MaxValue)]
        [InlineData("ulong.MaxValue+1 (2^64) becomes 0", 0UL, 0UL, 1UL, 0UL, 0UL)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, 42UL)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue)]
        public void Given_Boundary_When_ToULong_Then_MatchesTheExplicitULongCast(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, ulong expected)
            => AssertExact(caseName, expected, U(u3, u2, u1, u0).ToULong());

        [Fact]
        public void Given_TwoPow63_When_CastToLong_Then_ResultIsLongMinValue()
        {
            var v = U(0, 0, 0, 1UL << 63);
            AssertExact("2^63 -> long", long.MinValue, (long)v);
            Assert.True((long)v < 0, "2^63 must narrow to a NEGATIVE long, not a large one");
        }

        [Fact]
        public void Given_TwoPow64Minus1_When_CastToLong_Then_ResultIsMinusOne()
            => AssertExact("2^64-1 -> long", -1L, (long)U(0, 0, 0, ulong.MaxValue));

        [Fact]
        public void Given_TwoPow31_When_CastToInt_Then_ResultIsIntMinValue()
            => AssertExact("2^31 -> int", int.MinValue, (int)U(0, 0, 0, 1UL << 31));

        [Fact]
        public void Given_TwoPow64_When_CastToULong_Then_ResultIsZero()
        {
            var twoPow64 = U(0, 0, 1, 0);
            Assert.False(twoPow64.IsZero, "2^64 is not zero; only its low limb is");
            AssertExact("2^64 -> ulong", 0UL, (ulong)twoPow64);
        }

        [Fact]
        public void Given_LongMaxValueAndLongMaxValuePlusOne_When_ToLongSafe_Then_BothProduceLongMaxValueIndistinguishably()
        {
            var exactlyMax = U(0, 0, 0, (ulong)long.MaxValue);
            var oneAbove = U(0, 0, 0, (ulong)long.MaxValue + 1);

            AssertExact("long.MaxValue -> ToLongSafe", long.MaxValue, exactlyMax.ToLongSafe());
            AssertExact("long.MaxValue+1 -> ToLongSafe", long.MaxValue, oneAbove.ToLongSafe());

            Assert.NotEqual(exactlyMax, oneAbove);
            Assert.Equal(exactlyMax.ToLongSafe(), oneAbove.ToLongSafe());
        }

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, 0L)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, 1L)]
        [InlineData("long.MaxValue exact", 0UL, 0UL, 0UL, 9223372036854775807UL, long.MaxValue)]
        [InlineData("long.MaxValue+1 saturates", 0UL, 0UL, 0UL, 9223372036854775808UL, long.MaxValue)]
        [InlineData("2^64-1 saturates", 0UL, 0UL, 0UL, ulong.MaxValue, long.MaxValue)]
        [InlineData("2^64 saturates despite u0 zero", 0UL, 0UL, 1UL, 0UL, long.MaxValue)]
        [InlineData("u1 set, u0 looks innocent, saturates", 0UL, 0UL, 1UL, 42UL, long.MaxValue)]
        [InlineData("u2 set, u0 looks innocent, saturates", 0UL, 1UL, 0UL, 42UL, long.MaxValue)]
        [InlineData("u3 set, u0 looks innocent, saturates", 1UL, 0UL, 0UL, 42UL, long.MaxValue)]
        [InlineData("2^256-1 saturates", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, long.MaxValue)]
        public void Given_Boundary_When_ToLongSafe_Then_SaturatesAtLongMaxValueAndNeverGoesNegative(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, long expected)
        {
            var actual = U(u3, u2, u1, u0).ToLongSafe();
            AssertExact(caseName, expected, actual);
            Assert.True(actual >= 0, caseName + ": ToLongSafe must never produce a negative, produced " + actual);
        }

        [Fact]
        public void Given_ValueThatDoesNotFit_When_ToLongSafe_Then_ItSaturatesRatherThanThrowing()
        {
            var ex = Record.Exception(() => EvmUInt256.MaxValue.ToLongSafe());
            Assert.Null(ex);
            AssertExact("2^256-1 -> ToLongSafe", long.MaxValue, EvmUInt256.MaxValue.ToLongSafe());
        }

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, true)]
        [InlineData("ulong.MaxValue", 0UL, 0UL, 0UL, ulong.MaxValue, true)]
        [InlineData("2^64", 0UL, 0UL, 1UL, 0UL, false)]
        [InlineData("2^64+42", 0UL, 0UL, 1UL, 42UL, false)]
        [InlineData("u2 set", 0UL, 1UL, 0UL, 0UL, false)]
        [InlineData("u3 set", 1UL, 0UL, 0UL, 0UL, false)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, false)]
        public void Given_Boundary_When_FitsInULong_Then_ItAgreesWithWhetherTheULongCastIsLossless(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, bool expectedFits)
        {
            var v = U(u3, u2, u1, u0);
            AssertExact(caseName + " FitsInULong", expectedFits, v.FitsInULong);

            var lossless = new EvmUInt256((ulong)v) == v;
            Assert.True(v.FitsInULong == lossless,
                caseName + ": FitsInULong=" + v.FitsInULong + " but the ulong cast lossless=" + lossless);
        }

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, true)]
        [InlineData("int.MaxValue", 0UL, 0UL, 0UL, 2147483647UL, true)]
        [InlineData("int.MaxValue+1", 0UL, 0UL, 0UL, 2147483648UL, false)]
        [InlineData("2^32", 0UL, 0UL, 0UL, 4294967296UL, false)]
        [InlineData("ulong.MaxValue", 0UL, 0UL, 0UL, ulong.MaxValue, false)]
        [InlineData("2^64 (u0 is zero and looks like it fits)", 0UL, 0UL, 1UL, 0UL, false)]
        [InlineData("2^64+42 (u0 looks innocent)", 0UL, 0UL, 1UL, 42UL, false)]
        [InlineData("u3 set, u0 looks innocent", 1UL, 0UL, 0UL, 42UL, false)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, false)]
        public void Given_Boundary_When_FitsInInt_Then_ItAgreesWithWhetherTheIntCastIsLossless(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, bool expectedFits)
        {
            var v = U(u3, u2, u1, u0);
            AssertExact(caseName + " FitsInInt", expectedFits, v.FitsInInt);

            var narrowed = (int)v;
            var lossless = narrowed >= 0 && new EvmUInt256((ulong)narrowed) == v;
            Assert.True(v.FitsInInt == lossless,
                caseName + ": FitsInInt=" + v.FitsInInt + " but the int cast produced " + narrowed + ", lossless=" + lossless);
        }

        [Fact]
        public void Given_ValueAboveLongMaxValue_When_FitsInULongIsTrue_Then_TheLongCastIsStillNegative()
        {
            var v = U(0, 0, 0, ulong.MaxValue);
            Assert.True(v.FitsInULong);
            AssertExact("ulong.MaxValue -> long", -1L, (long)v);
            AssertExact("ulong.MaxValue -> ToLongSafe", long.MaxValue, v.ToLongSafe());
        }

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL, false)]
        [InlineData("one", 0UL, 0UL, 0UL, 1UL, false)]
        [InlineData("2^64", 0UL, 0UL, 1UL, 0UL, false)]
        [InlineData("2^255 exactly", 9223372036854775808UL, 0UL, 0UL, 0UL, true)]
        [InlineData("2^255-1", 9223372036854775807UL, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, false)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, true)]
        [InlineData("u3 nonzero but high bit clear", 1UL, 0UL, 0UL, 0UL, false)]
        public void Given_Boundary_When_IsHighBitSet_Then_OnlyBit255Decides(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0, bool expected)
            => AssertExact(caseName, expected, U(u3, u2, u1, u0).IsHighBitSet);

        [Fact]
        public void Given_NegativeInt_When_WidenedToEvmUInt256_Then_ItSignExtendsAndOnlyTheSignedCastsRoundTrip()
        {
            EvmUInt256 v = -1;
            Assert.Equal(EvmUInt256.MaxValue, v);

            AssertExact("-1 -> int", -1, (int)v);
            AssertExact("-1 -> long", -1L, (long)v);
            AssertExact("-1 -> ulong", ulong.MaxValue, (ulong)v);
            AssertExact("-1 -> uint", uint.MaxValue, (uint)v);
            AssertExact("-1 -> ushort", (ushort)65535, (ushort)v);
            AssertExact("-1 -> byte", (byte)255, (byte)v);
        }

        [Fact]
        public void Given_LongMinValue_When_WidenedToEvmUInt256_Then_ItNarrowsBackToLongMinValueButToLongSafeSaturates()
        {
            EvmUInt256 v = long.MinValue;
            AssertExact("long.MinValue -> long", long.MinValue, (long)v);
            AssertExact("long.MinValue -> ToLongSafe", long.MaxValue, v.ToLongSafe());
            Assert.False(v.FitsInULong, "sign extension fills the high limbs, so it cannot fit in a ulong");
        }

        [Fact]
        public void Given_SmallNegativeEvmInt256_When_CastToLong_Then_TheValueRoundTrips()
        {
            EvmInt256 v = -3000000L;
            AssertExact("-3000000 -> long", -3000000L, (long)v);
            AssertExact("-3000000 -> int", -3000000, (int)v);
        }

        [Fact]
        public void Given_EvmInt256MinValue_When_CastToLong_Then_ItNarrowsToZeroNotLongMinValue()
        {
            AssertExact("EvmInt256.MinValue -> long", 0L, (long)EvmInt256.MinValue);
            AssertExact("EvmInt256.MinValue -> int", 0, (int)EvmInt256.MinValue);
            Assert.True(EvmInt256.MinValue.IsNegative, "the source value is negative, the narrowed value is not");
        }

        [Fact]
        public void Given_EvmInt256MaxValue_When_CastToLong_Then_ItNarrowsToMinusOne()
        {
            AssertExact("EvmInt256.MaxValue -> long", -1L, (long)EvmInt256.MaxValue);
            AssertExact("EvmInt256.MaxValue -> int", -1, (int)EvmInt256.MaxValue);
        }

        [Fact]
        public void Given_NegativeBeyondLongRange_When_CastToLong_Then_ItNarrowsToMinusOne()
        {
            var v = (EvmInt256)U(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue - 1, ulong.MaxValue);
            Assert.True(v.IsNegative);
            AssertExact("-(2^64+1) -> long", -1L, (long)v);
        }

        [Fact]
        public void Given_EvmInt256_When_RoundTrippedThroughEvmUInt256_Then_TheBitsArePreserved()
        {
            var bits = U(0x0123456789ABCDEFUL, ulong.MaxValue, 1UL, 42UL);
            var signed = (EvmInt256)bits;
            Assert.Equal(bits, (EvmUInt256)signed);
        }

        [Fact]
        public void Given_ThirtyThreeByteInput_When_FromBigEndian_Then_TheLeadingByteIsDroppedAndTheLow32Survive()
        {
            var bytes = new byte[33];
            bytes[0] = 0xAA;
            bytes[32] = 0x01;
            var v = EvmUInt256.FromBigEndian(bytes);

            AssertExact("FromBigEndian(33) low limb", 1UL, v.U0);
            AssertExact("FromBigEndian(33) high limb", 0UL, v.U3);
        }

        [Fact]
        public void Given_ThirtyThreeByteInput_When_ToEvmUInt256FromRLPDecoded_Then_TheTrailingByteIsDroppedInstead()
        {
            var bytes = new byte[33];
            bytes[0] = 0xAA;
            bytes[32] = 0x01;
            var v = bytes.ToEvmUInt256FromRLPDecoded();

            AssertExact("RLP(33) high limb", 0xAA00000000000000UL, v.U3);
            AssertExact("RLP(33) low limb", 0UL, v.U0);
        }

        [Fact]
        public void Given_NineByteRlpScalar_When_Decoded_Then_ItYieldsAValueThatNoNarrowingCastCanRepresent()
        {
            var nineBytes = new byte[] { 0x01, 0, 0, 0, 0, 0, 0, 0, 0 };
            var v = nineBytes.ToEvmUInt256FromRLPDecoded();

            Assert.Equal(U(0, 0, 1, 0), v);
            Assert.False(v.FitsInULong);
            AssertExact("9-byte scalar -> ulong", 0UL, (ulong)v);
            AssertExact("9-byte scalar -> long", 0L, (long)v);
            AssertExact("9-byte scalar -> ToLongSafe", long.MaxValue, v.ToLongSafe());
        }

        [Theory]
        [InlineData("zero", 0UL, 0UL, 0UL, 0UL)]
        [InlineData("ulong.MaxValue", 0UL, 0UL, 0UL, ulong.MaxValue)]
        [InlineData("2^64", 0UL, 0UL, 1UL, 0UL)]
        [InlineData("u3 set", 1UL, 0UL, 0UL, 42UL)]
        [InlineData("2^256-1", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue)]
        public void Given_Boundary_When_RoundTrippedThroughBigEndianBytes_Then_NothingIsLost(
            string caseName, ulong u3, ulong u2, ulong u1, ulong u0)
        {
            var v = U(u3, u2, u1, u0);
            var bytes = v.ToBigEndian();
            Assert.True(bytes.Length == 32, caseName + ": expected 32 bytes, got " + bytes.Length);
            Assert.True(EvmUInt256.FromBigEndian(bytes) == v, caseName + ": 32-byte round trip must be lossless");
        }

        [Fact]
        public void Given_BigIntegerAtTwoPow256_When_ConvertedToEvmUInt256_Then_ItWrapsSilentlyToZero()
        {
            var v = new EvmUInt256(BigInteger.One << 256);
            Assert.True(v.IsZero, "2^256 must wrap to zero, produced " + v);
        }

        [Fact]
        public void Given_BigIntegerAtTwoPow256Plus1_When_ConvertedToEvmUInt256_Then_ItWrapsSilentlyToOne()
        {
            var v = new EvmUInt256((BigInteger.One << 256) + 1);
            Assert.True(v == EvmUInt256.One, "2^256+1 must wrap to one, produced " + v);
        }

        [Fact]
        public void Given_MaxUInt256AsBigInteger_When_ConvertedAndBack_Then_ItIsLossless()
        {
            var big = (BigInteger.One << 256) - 1;
            var v = new EvmUInt256(big);
            Assert.Equal(EvmUInt256.MaxValue, v);
            Assert.Equal(big, v.ToBigInteger());
        }
    }
}
