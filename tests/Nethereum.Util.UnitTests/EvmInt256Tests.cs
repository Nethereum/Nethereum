using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class EvmInt256Tests
    {
        static readonly BigInteger TWO_256 = BigInteger.Pow(2, 256);
        static readonly BigInteger INT256_MAX = BigInteger.Pow(2, 255) - 1;
        static readonly BigInteger INT256_MIN = -BigInteger.Pow(2, 255);

        private static EvmInt256 FromBigInteger(BigInteger bi)
        {
            if (bi < 0) bi += TWO_256;
            return (EvmInt256)EvmUInt256BigIntegerExtensions.FromBigInteger(bi);
        }

        private static BigInteger ToBigIntegerSigned(EvmInt256 v)
        {
            var bi = ((EvmUInt256)v).ToBigInteger();
            if (bi >= BigInteger.Pow(2, 255)) bi -= TWO_256;
            return bi;
        }

        // === Construction ===

        [Fact]
        public void Ctor_Positive()
        {
            EvmInt256 v = 42;
            Assert.False(v.IsNegative);
            Assert.False(v.IsZero);
            Assert.True(v.IsPositive);
            Assert.Equal(42UL, v.U0);
        }

        [Fact]
        public void Ctor_Negative()
        {
            EvmInt256 v = -1;
            Assert.True(v.IsNegative);
            Assert.False(v.IsZero);
            Assert.False(v.IsPositive);
        }

        [Fact]
        public void Ctor_Zero()
        {
            EvmInt256 v = 0;
            Assert.True(v.IsZero);
            Assert.False(v.IsNegative);
            Assert.False(v.IsPositive);
        }

        [Fact]
        public void Ctor_Long_3000000()
        {
            EvmInt256 v = 3000000L;
            Assert.Equal(3000000UL, v.U0);
            Assert.Equal(0UL, v.U1);
            Assert.Equal(3000000L, (long)v);
        }

        [Fact]
        public void Ctor_Long_Negative3000000()
        {
            EvmInt256 v = -3000000L;
            Assert.True(v.IsNegative);
            Assert.Equal("-3000000", v.ToString());
        }

        // === Constants ===

        [Fact]
        public void MinValue_IsNegative()
        {
            Assert.True(EvmInt256.MinValue.IsNegative);
            Assert.Equal(INT256_MIN, ToBigIntegerSigned(EvmInt256.MinValue));
        }

        [Fact]
        public void MaxValue_IsPositive()
        {
            Assert.True(EvmInt256.MaxValue.IsPositive);
            Assert.Equal(INT256_MAX, ToBigIntegerSigned(EvmInt256.MaxValue));
        }

        [Fact]
        public void MinusOne()
        {
            Assert.True(EvmInt256.MinusOne.IsNegative);
            Assert.Equal(-1, ToBigIntegerSigned(EvmInt256.MinusOne));
        }

        // === Abs ===

        [Fact]
        public void Abs_Positive()
        {
            EvmInt256 v = 42;
            Assert.Equal(new EvmUInt256(42), v.Abs());
        }

        [Fact]
        public void Abs_Negative()
        {
            EvmInt256 v = -42;
            Assert.Equal(new EvmUInt256(42), v.Abs());
        }

        [Fact]
        public void Abs_Zero()
        {
            Assert.Equal(EvmUInt256.Zero, EvmInt256.Zero.Abs());
        }

        // === Arithmetic ===

        [Fact]
        public void Add_PositivePositive()
        {
            EvmInt256 a = 3;
            EvmInt256 b = 5;
            EvmInt256 r = a + b;
            Assert.Equal(8L, (long)r);
        }

        [Fact]
        public void Add_PositiveNegative()
        {
            EvmInt256 a = 10;
            EvmInt256 b = -3;
            EvmInt256 r = a + b;
            Assert.Equal(7L, (long)r);
        }

        [Fact]
        public void Sub_Basic()
        {
            EvmInt256 a = 10;
            EvmInt256 b = 3;
            Assert.Equal(7L, (long)(a - b));
        }

        [Fact]
        public void Sub_GoesNegative()
        {
            EvmInt256 a = 3;
            EvmInt256 b = 10;
            EvmInt256 r = a - b;
            Assert.True(r.IsNegative);
            Assert.Equal("-7", r.ToString());
        }

        [Fact]
        public void Negate()
        {
            EvmInt256 a = 42;
            EvmInt256 neg = -a;
            Assert.True(neg.IsNegative);
            Assert.Equal("-42", neg.ToString());
            Assert.Equal(EvmInt256.Zero, a + neg);
        }

        [Fact]
        public void Multiply_PositivePositive()
        {
            EvmInt256 a = 6;
            EvmInt256 b = 7;
            Assert.Equal(42L, (long)(a * b));
        }

        [Fact]
        public void Multiply_PositiveNegative()
        {
            EvmInt256 a = 6;
            EvmInt256 b = -7;
            EvmInt256 r = a * b;
            Assert.True(r.IsNegative);
            Assert.Equal("-42", r.ToString());
        }

        [Fact]
        public void Multiply_NegativeNegative()
        {
            EvmInt256 a = -6;
            EvmInt256 b = -7;
            EvmInt256 r = a * b;
            Assert.False(r.IsNegative);
            Assert.Equal(42L, (long)r);
        }

        // === Signed Division (SDIV) ===

        [Fact]
        public void Div_PositivePositive()
        {
            EvmInt256 a = 42;
            EvmInt256 b = 6;
            Assert.Equal(7L, (long)(a / b));
        }

        [Fact]
        public void Div_NegativePositive()
        {
            EvmInt256 a = -42;
            EvmInt256 b = 6;
            Assert.Equal("-7", (a / b).ToString());
        }

        [Fact]
        public void Div_PositiveNegative()
        {
            EvmInt256 a = 42;
            EvmInt256 b = -6;
            Assert.Equal("-7", (a / b).ToString());
        }

        [Fact]
        public void Div_NegativeNegative()
        {
            EvmInt256 a = -42;
            EvmInt256 b = -6;
            Assert.Equal(7L, (long)(a / b));
        }

        [Fact]
        public void Div_ByZero_ReturnsZero()
        {
            EvmInt256 a = 42;
            EvmInt256 b = 0;
            Assert.Equal(EvmInt256.Zero, a / b);
        }

        [Fact]
        public void Div_MinValue_ByMinusOne_ReturnsMinValue()
        {
            // EVM SDIV special case: INT256_MIN / -1 = INT256_MIN (overflow wraps)
            var r = EvmInt256.MinValue / EvmInt256.MinusOne;
            Assert.Equal(EvmInt256.MinValue, r);
        }

        // === Signed Modulo (SMOD) ===

        [Fact]
        public void Mod_Positive()
        {
            EvmInt256 a = 10;
            EvmInt256 b = 3;
            Assert.Equal(1L, (long)(a % b));
        }

        [Fact]
        public void Mod_NegativeDividend()
        {
            // -10 % 3 = -1 (sign follows dividend)
            EvmInt256 a = -10;
            EvmInt256 b = 3;
            Assert.Equal("-1", (a % b).ToString());
        }

        [Fact]
        public void Mod_NegativeDivisor()
        {
            // 10 % -3 = 1 (sign follows dividend, which is positive)
            EvmInt256 a = 10;
            EvmInt256 b = -3;
            Assert.Equal(1L, (long)(a % b));
        }

        [Fact]
        public void Mod_ByZero_ReturnsZero()
        {
            Assert.Equal(EvmInt256.Zero, new EvmInt256(10L) % EvmInt256.Zero);
        }

        // === Signed Comparison ===

        [Fact]
        public void Compare_PositivePositive()
        {
            Assert.True(new EvmInt256(3L) < new EvmInt256(5L));
            Assert.False(new EvmInt256(5L) < new EvmInt256(3L));
        }

        [Fact]
        public void Compare_NegativeNegative()
        {
            Assert.True(new EvmInt256(-5L) < new EvmInt256(-3L));
            Assert.False(new EvmInt256(-3L) < new EvmInt256(-5L));
        }

        [Fact]
        public void Compare_NegativePositive()
        {
            Assert.True(new EvmInt256(-1L) < new EvmInt256(1L));
            Assert.False(new EvmInt256(1L) < new EvmInt256(-1L));
        }

        [Fact]
        public void Compare_Zero()
        {
            Assert.True(new EvmInt256(-1L) < EvmInt256.Zero);
            Assert.True(EvmInt256.Zero < new EvmInt256(1L));
        }

        [Fact]
        public void Compare_MinMax()
        {
            Assert.True(EvmInt256.MinValue < EvmInt256.MaxValue);
            Assert.True(EvmInt256.MinValue < EvmInt256.Zero);
            Assert.True(EvmInt256.Zero < EvmInt256.MaxValue);
        }

        [Fact]
        public void Compare_Equality()
        {
            Assert.True(new EvmInt256(42L) == new EvmInt256(42L));
            Assert.True(new EvmInt256(-1L) == EvmInt256.MinusOne);
        }

        [Fact]
        public void Compare_WithLong()
        {
            EvmInt256 v = 5;
            Assert.True(v > 3L);
            Assert.True(v < 10L);
            Assert.True(v >= 5L);
            Assert.True(v <= 5L);
            Assert.True(v == 5L);
            Assert.True(v != 4L);
        }

        // === Increment/Decrement ===

        [Fact]
        public void Increment()
        {
            EvmInt256 v = 5;
            v++;
            Assert.Equal(6L, (long)v);
        }

        [Fact]
        public void Decrement()
        {
            EvmInt256 v = 5;
            v--;
            Assert.Equal(4L, (long)v);
        }

        [Fact]
        public void Decrement_ZeroBecomesMinus1()
        {
            EvmInt256 v = 0;
            v--;
            Assert.Equal(EvmInt256.MinusOne, v);
            Assert.True(v.IsNegative);
        }

        [Fact]
        public void Increment_Minus1BecomesZero()
        {
            EvmInt256 v = -1;
            v++;
            Assert.Equal(EvmInt256.Zero, v);
        }

        // === Shifts ===

        [Fact]
        public void ShiftLeft()
        {
            EvmInt256 v = 1;
            Assert.Equal(16L, (long)(v << 4));
        }

        [Fact]
        public void ArithmeticShiftRight_Positive()
        {
            EvmInt256 v = 16;
            Assert.Equal(4L, (long)(v >> 2));
        }

        [Fact]
        public void ArithmeticShiftRight_Negative_PreservesSign()
        {
            EvmInt256 v = -1;
            EvmInt256 shifted = v >> 1;
            Assert.Equal(EvmInt256.MinusOne, shifted); // -1 >> any = -1
        }

        [Fact]
        public void ArithmeticShiftRight_NegativeValue()
        {
            EvmInt256 v = -16;
            EvmInt256 shifted = v >> 2;
            Assert.Equal("-4", shifted.ToString());
        }

        // === Min/Max ===

        [Fact]
        public void Min_Max()
        {
            EvmInt256 a = -5;
            EvmInt256 b = 3;
            Assert.Equal(a, EvmInt256.Min(a, b));
            Assert.Equal(b, EvmInt256.Max(a, b));
        }

        // === ToString ===

        [Fact]
        public void ToString_Positive() => Assert.Equal("42", new EvmInt256(42L).ToString());

        [Fact]
        public void ToString_Negative() => Assert.Equal("-42", new EvmInt256(-42L).ToString());

        [Fact]
        public void ToString_Zero() => Assert.Equal("0", EvmInt256.Zero.ToString());

        [Fact]
        public void ToString_MinusOne() => Assert.Equal("-1", EvmInt256.MinusOne.ToString());

        // === Gas Calculation Scenarios ===

        [Fact]
        public void GasCalculation_SubtractToNegative()
        {
            // Simulates: gasRemaining = 100, gasCost = 150 → negative
            EvmInt256 remaining = 100;
            EvmInt256 cost = 150;
            var result = remaining - cost;
            Assert.True(result.IsNegative);
            Assert.Equal("-50", result.ToString());
            Assert.True(result < 0L);
        }

        [Fact]
        public void GasCalculation_3000000_Roundtrip()
        {
            EvmInt256 gasLimit = 3000000L;
            Assert.Equal(3000000L, (long)gasLimit);
            Assert.Equal("3000000", gasLimit.ToString());
            Assert.False(gasLimit.IsNegative);
            Assert.True(gasLimit > 0L);
        }

        [Fact]
        public void GasCalculation_Refund()
        {
            EvmInt256 gasUsed = 21000L;
            EvmInt256 refund = 5000L;
            var effective = gasUsed - refund;
            Assert.Equal(16000L, (long)effective);
        }

        // === Conversion Roundtrip ===

        [Fact]
        public void EvmUInt256_Roundtrip()
        {
            var unsigned = new EvmUInt256(42);
            var signed = (EvmInt256)unsigned;
            var back = (EvmUInt256)signed;
            Assert.Equal(unsigned, back);
        }

        [Fact]
        public void EvmUInt256_NegativeRoundtrip()
        {
            EvmInt256 neg = -42;
            var unsigned = (EvmUInt256)neg;
            var signed = (EvmInt256)unsigned;
            Assert.Equal(neg, signed);
            Assert.Equal("-42", signed.ToString());
        }

        // === CompareTo ===

        [Fact]
        public void CompareTo_Ordering()
        {
            Assert.True(new EvmInt256(-1L).CompareTo(new EvmInt256(1L)) < 0);
            Assert.True(new EvmInt256(1L).CompareTo(new EvmInt256(-1L)) > 0);
            Assert.Equal(0, new EvmInt256(42L).CompareTo(new EvmInt256(42L)));
        }

        // === Fuzz: Signed operations match BigInteger ===

        [Fact]
        public void SignedCompare_Fuzz()
        {
            var rng = new System.Random(55);
            for (int i = 0; i < 500; i++)
            {
                long a = (long)(rng.NextDouble() * (double)long.MaxValue * 2) - long.MaxValue;
                long b = (long)(rng.NextDouble() * (double)long.MaxValue * 2) - long.MaxValue;
                var ea = new EvmInt256(a);
                var eb = new EvmInt256(b);
                Assert.Equal(a < b, ea < eb);
                Assert.Equal(a > b, ea > eb);
                Assert.Equal(a == b, ea == eb);
            }
        }

        [Fact]
        public void SignedDiv_Fuzz()
        {
            var rng = new System.Random(66);
            for (int i = 0; i < 500; i++)
            {
                long a = (long)(rng.NextDouble() * 2000000) - 1000000;
                long b = (long)(rng.NextDouble() * 2000000) - 1000000;
                if (b == 0) b = 1;
                var ea = new EvmInt256(a);
                var eb = new EvmInt256(b);
                long expected = a / b;
                Assert.Equal(expected, (long)(ea / eb));
            }
        }

        [Fact]
        public void SignedMod_Fuzz()
        {
            var rng = new System.Random(77);
            for (int i = 0; i < 500; i++)
            {
                long a = (long)(rng.NextDouble() * 2000000) - 1000000;
                long b = (long)(rng.NextDouble() * 2000000) - 1000000;
                if (b == 0) b = 1;
                var ea = new EvmInt256(a);
                var eb = new EvmInt256(b);
                long expected = a % b;
                Assert.Equal(expected, (long)(ea % eb));
            }
        }
    }
}
