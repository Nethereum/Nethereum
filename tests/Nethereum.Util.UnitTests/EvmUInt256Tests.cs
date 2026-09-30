using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class EvmUInt256Tests
    {
        // === Constructors ===

        [Fact]
        public void Ctor_Ulong_SetsU0Only()
        {
            var v = new EvmUInt256(42UL);
            Assert.Equal(42UL, v.U0);
            Assert.Equal(0UL, v.U1);
            Assert.Equal(0UL, v.U2);
            Assert.Equal(0UL, v.U3);
        }

        [Fact]
        public void Ctor_Long_Positive()
        {
            var v = new EvmUInt256(3000000L);
            Assert.Equal(3000000UL, v.U0);
            Assert.Equal(0UL, v.U1);
            Assert.Equal(0UL, v.U2);
            Assert.Equal(0UL, v.U3);
        }

        [Fact]
        public void Ctor_Long_Negative_SignExtends()
        {
            var v = new EvmUInt256(-1L);
            Assert.Equal(ulong.MaxValue, v.U0);
            Assert.Equal(ulong.MaxValue, v.U1);
            Assert.Equal(ulong.MaxValue, v.U2);
            Assert.Equal(ulong.MaxValue, v.U3);
        }

        [Fact]
        public void Ctor_FourLimbs()
        {
            var v = new EvmUInt256(0xA, 0xB, 0xC, 0xD);
            Assert.Equal(0xDUL, v.U0);
            Assert.Equal(0xCUL, v.U1);
            Assert.Equal(0xBUL, v.U2);
            Assert.Equal(0xAUL, v.U3);
        }

        // === Implicit Conversions ===

        [Fact]
        public void ImplicitFromInt_Positive()
        {
            EvmUInt256 v = 3000000;
            Assert.Equal(3000000UL, v.U0);
            Assert.Equal(0UL, v.U1);
            Assert.Equal(0UL, v.U2);
            Assert.Equal(0UL, v.U3);
        }

        [Fact]
        public void ImplicitFromInt_Zero()
        {
            EvmUInt256 v = 0;
            Assert.True(v.IsZero);
        }

        [Fact]
        public void ImplicitFromInt_Negative()
        {
            EvmUInt256 v = -1;
            Assert.Equal(EvmUInt256.MaxValue, v);
        }

        [Fact]
        public void ImplicitFromLong_Positive()
        {
            EvmUInt256 v = 3000000L;
            Assert.Equal(3000000UL, v.U0);
            Assert.Equal(0UL, v.U1);
            Assert.Equal(0UL, v.U2);
            Assert.Equal(0UL, v.U3);
        }

        [Fact]
        public void ImplicitFromLong_LargePositive()
        {
            EvmUInt256 v = long.MaxValue;
            Assert.Equal((ulong)long.MaxValue, v.U0);
            Assert.Equal(0UL, v.U1);
        }

        [Fact]
        public void ImplicitFromUlong()
        {
            EvmUInt256 v = ulong.MaxValue;
            Assert.Equal(ulong.MaxValue, v.U0);
            Assert.Equal(0UL, v.U1);
        }

        // === Explicit Conversions ===

        [Fact]
        public void ExplicitToInt()
        {
            var v = new EvmUInt256(42);
            Assert.Equal(42, (int)v);
        }

        [Fact]
        public void ExplicitToLong()
        {
            var v = new EvmUInt256(3000000);
            Assert.Equal(3000000L, (long)v);
        }

        [Fact]
        public void ExplicitToUlong()
        {
            var v = new EvmUInt256(ulong.MaxValue);
            Assert.Equal(ulong.MaxValue, (ulong)v);
        }

        [Fact]
        public void CastLong3000000_Bug()
        {
            EvmUInt256 v = (long)3000000;
            Assert.Equal(3000000UL, v.U0);
            Assert.Equal(0UL, v.U1);
            Assert.Equal(0UL, v.U2);
            Assert.Equal(0UL, v.U3);
            Assert.Equal(3000000L, (long)v);
        }

        // === BigInteger Roundtrip ===

        [Fact]
        public void BigInteger_Roundtrip_Small()
        {
            BigInteger bi = 3000000;
            var v = EvmUInt256BigIntegerExtensions.FromBigInteger(bi);
            Assert.Equal(3000000UL, v.U0);
            Assert.Equal(bi, v.ToBigInteger());
        }

        [Fact]
        public void BigInteger_Roundtrip_MaxValue()
        {
            var bi = BigInteger.Pow(2, 256) - 1;
            var v = EvmUInt256BigIntegerExtensions.FromBigInteger(bi);
            Assert.Equal(EvmUInt256.MaxValue, v);
            Assert.Equal(bi, v.ToBigInteger());
        }

        [Fact]
        public void BigInteger_Roundtrip_Negative()
        {
            BigInteger bi = -1;
            var v = EvmUInt256BigIntegerExtensions.FromBigInteger(bi);
            Assert.Equal(EvmUInt256.MaxValue, v);
        }

        [Fact]
        public void BigInteger_Roundtrip_Powers()
        {
            for (int p = 0; p < 256; p++)
            {
                var bi = BigInteger.One << p;
                var v = EvmUInt256BigIntegerExtensions.FromBigInteger(bi);
                var rt = v.ToBigInteger();
                Assert.Equal(bi, rt);
            }
        }

        // === Hex/FromHex ===

        [Fact]
        public void FromHex_SimpleValues()
        {
            Assert.Equal(new EvmUInt256(0), EvmUInt256.FromHex("0x0"));
            Assert.Equal(new EvmUInt256(255), EvmUInt256.FromHex("0xff"));
            Assert.Equal(new EvmUInt256(256), EvmUInt256.FromHex("0x100"));
        }

        [Fact]
        public void ToHexString_Values()
        {
            Assert.Equal("0x0", EvmUInt256.Zero.ToHexString());
            Assert.Equal("0x01", EvmUInt256.One.ToHexString());
            Assert.Equal("0x2a", new EvmUInt256(42).ToHexString());
        }

        // === Byte Array Roundtrip ===

        [Fact]
        public void BigEndian_Roundtrip_Fuzz()
        {
            var rng = new System.Random(123);
            for (int i = 0; i < 100; i++)
            {
                var bytes = new byte[32];
                rng.NextBytes(bytes);
                var v = EvmUInt256.FromBigEndian(bytes);
                var rt = v.ToBigEndian();
                Assert.Equal(bytes, rt);
            }
        }

        [Fact]
        public void BigEndian_ShortArray()
        {
            var v = EvmUInt256.FromBigEndian(new byte[] { 0x01, 0x00 });
            Assert.Equal(256UL, v.U0);
        }

        // === Addition ===

        [Fact]
        public void Add_Basic() => Assert.Equal(new EvmUInt256(8), new EvmUInt256(3) + new EvmUInt256(5));

        [Fact]
        public void Add_Overflow_Wraps() => Assert.Equal(EvmUInt256.Zero, EvmUInt256.MaxValue + EvmUInt256.One);

        [Fact]
        public void Add_CarryPropagation()
        {
            var a = new EvmUInt256(0, 0, 0, ulong.MaxValue);
            var b = new EvmUInt256(1);
            var r = a + b;
            Assert.Equal(0UL, r.U0);
            Assert.Equal(1UL, r.U1);
            Assert.Equal(0UL, r.U2);
            Assert.Equal(0UL, r.U3);
        }

        [Fact]
        public void Add_FullCarryChain()
        {
            var a = new EvmUInt256(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var b = EvmUInt256.One;
            var r = a + b;
            Assert.Equal(0UL, r.U0);
            Assert.Equal(0UL, r.U1);
            Assert.Equal(0UL, r.U2);
            Assert.Equal(1UL, r.U3);
        }

        // === Subtraction ===

        [Fact]
        public void Sub_Basic() => Assert.Equal(new EvmUInt256(7), new EvmUInt256(10) - new EvmUInt256(3));

        [Fact]
        public void Sub_Underflow_Wraps() => Assert.Equal(EvmUInt256.MaxValue, EvmUInt256.Zero - EvmUInt256.One);

        // === Multiplication ===

        [Fact]
        public void Mul_Basic() => Assert.Equal(new EvmUInt256(42), new EvmUInt256(6) * new EvmUInt256(7));

        [Fact]
        public void Mul_CrossLimb()
        {
            var a = new EvmUInt256(ulong.MaxValue);
            var r = a * new EvmUInt256(2);
            Assert.Equal(new EvmUInt256(0, 0, 1, ulong.MaxValue - 1), r);
        }

        [Fact]
        public void Mul_Overflow_2pow128_Squared()
        {
            var a = EvmUInt256.One << 128;
            Assert.Equal(EvmUInt256.Zero, a * a);
        }

        [Fact]
        public void Mul_Fuzz_MatchesBigInteger()
        {
            var two256 = BigInteger.Pow(2, 256);
            var rng = new System.Random(42);
            for (int i = 0; i < 1000; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);
                var aBi = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                var expectedBi = (aBi * bBi) % two256;
                var a = EvmUInt256.FromBigEndian(aBytes);
                var b = EvmUInt256.FromBigEndian(bBytes);
                var result = a * b;
                Assert.Equal(expectedBi, result.ToBigInteger());
            }
        }

        // === Division ===

        [Fact]
        public void Div_Basic() => Assert.Equal(new EvmUInt256(7), new EvmUInt256(42) / new EvmUInt256(6));

        [Fact]
        public void Div_ByZero_ReturnsZero() => Assert.Equal(EvmUInt256.Zero, new EvmUInt256(42) / EvmUInt256.Zero);

        [Fact]
        public void Div_SelfIsOne()
        {
            var v = new EvmUInt256(0xDEAD, 0xBEEF, 0xCAFE, 0xBABE);
            Assert.Equal(EvmUInt256.One, v / v);
        }

        [Fact]
        public void Div_Fuzz_MatchesBigInteger()
        {
            var rng = new System.Random(77);
            for (int i = 0; i < 200; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);
                if (bBytes[31] == 0) bBytes[31] = 1; // avoid div by zero
                var aBi = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                if (bBi.IsZero) continue;
                var expectedQ = aBi / bBi;
                var expectedR = aBi % bBi;
                var a = EvmUInt256.FromBigEndian(aBytes);
                var b = EvmUInt256.FromBigEndian(bBytes);
                Assert.Equal(expectedQ, (a / b).ToBigInteger());
                Assert.Equal(expectedR, (a % b).ToBigInteger());
            }
        }

        // === Modular Arithmetic ===

        [Fact]
        public void Mod_Basic() => Assert.Equal(new EvmUInt256(1), new EvmUInt256(10) % new EvmUInt256(3));

        [Fact]
        public void AddMod_Simple()
        {
            Assert.Equal(new EvmUInt256(0), EvmUInt256.AddMod(new EvmUInt256(3), new EvmUInt256(7), new EvmUInt256(5)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.AddMod(new EvmUInt256(5), new EvmUInt256(3), EvmUInt256.Zero));
        }

        [Fact]
        public void AddMod_Overflow()
        {
            var bi = (BigInteger.Pow(2, 256) - 1 + 1) % 3;
            var result = EvmUInt256.AddMod(EvmUInt256.MaxValue, EvmUInt256.One, new EvmUInt256(3));
            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(bi), result);
        }

        [Fact]
        public void MulMod_Simple()
        {
            Assert.Equal(new EvmUInt256(1), EvmUInt256.MulMod(new EvmUInt256(3), new EvmUInt256(7), new EvmUInt256(5)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.MulMod(new EvmUInt256(5), new EvmUInt256(3), EvmUInt256.Zero));
        }

        [Fact]
        public void MulMod_Large()
        {
            var maxBi = BigInteger.Pow(2, 256) - 1;
            var expected = (maxBi * maxBi) % 7;
            var result = EvmUInt256.MulMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, new EvmUInt256(7));
            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(expected), result);
        }

        [Fact]
        public void MulMod_Fuzz_MatchesBigInteger()
        {
            var rng = new System.Random(99);
            var two256 = BigInteger.Pow(2, 256);
            for (int i = 0; i < 200; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                var mBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);
                rng.NextBytes(mBytes);
                if (mBytes[31] == 0) mBytes[31] = 1;
                var aBi = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                var mBi = new BigInteger(mBytes, isUnsigned: true, isBigEndian: true);
                if (mBi.IsZero) continue;
                var expected = (aBi * bBi) % mBi;
                var a = EvmUInt256.FromBigEndian(aBytes);
                var b = EvmUInt256.FromBigEndian(bBytes);
                var m = EvmUInt256.FromBigEndian(mBytes);
                var result = EvmUInt256.MulMod(a, b, m);
                Assert.Equal(expected, result.ToBigInteger());
            }
        }

        [Fact]
        public void ModPow_Simple()
        {
            Assert.Equal(new EvmUInt256(24), EvmUInt256.ModPow(new EvmUInt256(2), new EvmUInt256(10), new EvmUInt256(1000)));
        }

        [Fact]
        public void ModPow_MatchesBigInteger()
        {
            var expected = BigInteger.ModPow(7, 123, 1000000007);
            var result = EvmUInt256.ModPow(new EvmUInt256(7), new EvmUInt256(123), new EvmUInt256(1000000007));
            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(expected), result);
        }

        // === BigMul ===

        [Fact]
        public void BigMul_Simple()
        {
            var upper = EvmUInt256.BigMul(new EvmUInt256(3), new EvmUInt256(7), out var lower);
            Assert.Equal(EvmUInt256.Zero, upper);
            Assert.Equal(new EvmUInt256(21), lower);
        }

        [Fact]
        public void BigMul_MaxTimesTwo()
        {
            var upper = EvmUInt256.BigMul(EvmUInt256.MaxValue, new EvmUInt256(2), out var lower);
            Assert.Equal(EvmUInt256.One, upper);
            Assert.Equal(EvmUInt256.MaxValue - EvmUInt256.One, lower);
        }

        // === Comparison ===

        [Fact]
        public void Comparison_LessThan()
        {
            Assert.True(new EvmUInt256(3) < new EvmUInt256(5));
            Assert.False(new EvmUInt256(5) < new EvmUInt256(3));
            Assert.False(new EvmUInt256(5) < new EvmUInt256(5));
        }

        [Fact]
        public void Comparison_GreaterThan()
        {
            Assert.True(new EvmUInt256(5) > new EvmUInt256(3));
            Assert.False(new EvmUInt256(3) > new EvmUInt256(5));
        }

        [Fact]
        public void Comparison_Equality()
        {
            Assert.True(new EvmUInt256(42) == new EvmUInt256(42));
            Assert.False(new EvmUInt256(42) == new EvmUInt256(43));
        }

        [Fact]
        public void Comparison_WithInt()
        {
            Assert.True(new EvmUInt256(5) > 3);
            Assert.True(new EvmUInt256(3) < 5);
            Assert.True(new EvmUInt256(5) >= 5);
            Assert.True(new EvmUInt256(5) <= 5);
            Assert.True(new EvmUInt256(5) == 5);
            Assert.True(new EvmUInt256(5) != 4);
        }

        // === Bitwise ===

        [Fact]
        public void Bitwise_And() => Assert.Equal(new EvmUInt256(0x0F), new EvmUInt256(0xFF) & new EvmUInt256(0x0F));

        [Fact]
        public void Bitwise_Or() => Assert.Equal(new EvmUInt256(0xFF), new EvmUInt256(0xF0) | new EvmUInt256(0x0F));

        [Fact]
        public void Bitwise_Xor() => Assert.Equal(new EvmUInt256(0xF0), new EvmUInt256(0xFF) ^ new EvmUInt256(0x0F));

        [Fact]
        public void Bitwise_Not()
        {
            Assert.Equal(EvmUInt256.MaxValue, ~EvmUInt256.Zero);
            Assert.Equal(EvmUInt256.Zero, ~EvmUInt256.MaxValue);
        }

        // === Shifts ===

        [Fact]
        public void ShiftLeft_Basic()
        {
            Assert.Equal(new EvmUInt256(16), EvmUInt256.One << 4);
            Assert.Equal(new EvmUInt256(0, 0, 1, 0), EvmUInt256.One << 64);
            Assert.Equal(new EvmUInt256(0, 1, 0, 0), EvmUInt256.One << 128);
            Assert.Equal(new EvmUInt256(1, 0, 0, 0), EvmUInt256.One << 192);
        }

        [Fact]
        public void ShiftLeft_Zero() => Assert.Equal(new EvmUInt256(42), new EvmUInt256(42) << 0);

        [Fact]
        public void ShiftLeft_255()
        {
            var r = EvmUInt256.One << 255;
            Assert.Equal(1UL << 63, r.U3);
            Assert.Equal(0UL, r.U2);
        }

        [Fact]
        public void ShiftRight_Basic()
        {
            Assert.Equal(new EvmUInt256(4), new EvmUInt256(16) >> 2);
            Assert.Equal(EvmUInt256.One, new EvmUInt256(0, 0, 1, 0) >> 64);
        }

        [Fact]
        public void ShiftRight_Zero() => Assert.Equal(new EvmUInt256(42), new EvmUInt256(42) >> 0);

        // === Properties ===

        [Fact]
        public void IsZero() => Assert.True(EvmUInt256.Zero.IsZero);

        [Fact]
        public void IsOne() => Assert.True(EvmUInt256.One.IsOne);

        [Fact]
        public void FitsInULong()
        {
            Assert.True(new EvmUInt256(42).FitsInULong);
            Assert.False(new EvmUInt256(0, 0, 1, 0).FitsInULong);
        }

        [Fact]
        public void FitsInInt()
        {
            Assert.True(new EvmUInt256(42).FitsInInt);
            Assert.False(new EvmUInt256((ulong)int.MaxValue + 1).FitsInInt);
        }

        [Fact]
        public void IsHighBitSet()
        {
            Assert.True(EvmUInt256.MaxValue.IsHighBitSet);
            Assert.False(EvmUInt256.One.IsHighBitSet);
        }

        // === BitLength ===

        [Fact]
        public void BitLength_Values()
        {
            Assert.Equal(0, EvmUInt256.Zero.BitLength());
            Assert.Equal(1, EvmUInt256.One.BitLength());
            Assert.Equal(8, new EvmUInt256(255).BitLength());
            Assert.Equal(9, new EvmUInt256(256).BitLength());
            Assert.Equal(64, new EvmUInt256(ulong.MaxValue).BitLength());
            Assert.Equal(65, new EvmUInt256(0, 0, 1, 0).BitLength());
            Assert.Equal(256, EvmUInt256.MaxValue.BitLength());
        }

        // === GetByte ===

        [Fact]
        public void GetByte_AllPositions()
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++) bytes[i] = (byte)(i + 1);
            var v = EvmUInt256.FromBigEndian(bytes);
            for (int i = 0; i < 32; i++)
                Assert.Equal(bytes[i], v.GetByte(i));
        }

        // === Negate ===

        [Fact]
        public void Negate_TwosComplement()
        {
            Assert.Equal(EvmUInt256.MaxValue, EvmUInt256.One.Negate());
            Assert.Equal(EvmUInt256.One, EvmUInt256.MaxValue.Negate());
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.Zero.Negate());
        }

        [Fact]
        public void UnaryMinus()
        {
            EvmUInt256 v = 5;
            var neg = -v;
            Assert.Equal(EvmUInt256.Zero, v + neg);
        }

        // === ArithmeticRightShift ===

        [Fact]
        public void ArithmeticRightShift_Positive()
        {
            var v = new EvmUInt256(16);
            Assert.Equal(new EvmUInt256(4), v.ArithmeticRightShift(2));
        }

        [Fact]
        public void ArithmeticRightShift_Negative()
        {
            var v = EvmUInt256.MaxValue; // -1 in two's complement
            Assert.Equal(EvmUInt256.MaxValue, v.ArithmeticRightShift(1));
        }

        // === ToString ===

        [Fact]
        public void ToDecimalString_Values()
        {
            Assert.Equal("0", EvmUInt256.Zero.ToString());
            Assert.Equal("1", EvmUInt256.One.ToString());
            Assert.Equal("42", new EvmUInt256(42).ToString());
            Assert.Equal("18446744073709551615", new EvmUInt256(ulong.MaxValue).ToString());
        }

        [Fact]
        public void ToDecimalString_MaxValue()
        {
            var expected = (BigInteger.Pow(2, 256) - 1).ToString();
            Assert.Equal(expected, EvmUInt256.MaxValue.ToString());
        }

        // === EXP (binary exponentiation) ===

        [Fact]
        public void Exp_MatchesBigInteger()
        {
            var two256 = BigInteger.Pow(2, 256);
            void CheckExp(ulong baseVal, ulong exp)
            {
                var expected = BigInteger.ModPow(baseVal, exp, two256);
                var b = new EvmUInt256(baseVal);
                var e = new EvmUInt256(exp);
                var result = EvmUInt256.One;
                while (!e.IsZero)
                {
                    if ((e.U0 & 1) == 1) result = result * b;
                    b = b * b;
                    e = e >> 1;
                }
                Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(expected), result);
            }
            CheckExp(2, 10);
            CheckExp(2, 255);
            CheckExp(3, 100);
            CheckExp(256, 1);
            CheckExp(0, 0);
            CheckExp(1, 1000);
        }

        // === Full Arithmetic Fuzz ===

        [Fact]
        public void Add_Fuzz_MatchesBigInteger()
        {
            var two256 = BigInteger.Pow(2, 256);
            var rng = new System.Random(11);
            for (int i = 0; i < 1000; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);
                var aBi = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                var expected = (aBi + bBi) % two256;
                var a = EvmUInt256.FromBigEndian(aBytes);
                var b = EvmUInt256.FromBigEndian(bBytes);
                Assert.Equal(expected, (a + b).ToBigInteger());
            }
        }

        [Fact]
        public void Sub_Fuzz_MatchesBigInteger()
        {
            var two256 = BigInteger.Pow(2, 256);
            var rng = new System.Random(22);
            for (int i = 0; i < 1000; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);
                var aBi = new BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                var expected = ((aBi - bBi) % two256 + two256) % two256;
                var a = EvmUInt256.FromBigEndian(aBytes);
                var b = EvmUInt256.FromBigEndian(bBytes);
                Assert.Equal(expected, (a - b).ToBigInteger());
            }
        }

        // === Conversion Roundtrip Fuzz ===

        [Fact]
        public void ConversionRoundtrip_IntValues()
        {
            int[] values = { 0, 1, -1, 42, 3000000, int.MaxValue, int.MinValue, -3000000 };
            foreach (var v in values)
            {
                EvmUInt256 evm = v;
                BigInteger bi = v >= 0 ? (BigInteger)v : BigInteger.Pow(2, 256) + v;
                Assert.Equal(bi, evm.ToBigInteger());
            }
        }

        [Fact]
        public void ConversionRoundtrip_LongValues()
        {
            long[] values = { 0, 1, -1, 3000000, long.MaxValue, long.MinValue };
            foreach (var v in values)
            {
                EvmUInt256 evm = v;
                BigInteger bi = v >= 0 ? (BigInteger)v : BigInteger.Pow(2, 256) + v;
                Assert.Equal(bi, evm.ToBigInteger());
            }
        }

        [Fact]
        public void ConversionRoundtrip_UlongValues()
        {
            ulong[] values = { 0, 1, 42, ulong.MaxValue, 3000000 };
            foreach (var v in values)
            {
                EvmUInt256 evm = v;
                Assert.Equal((BigInteger)v, evm.ToBigInteger());
            }
        }

        // === UInt128-Style Operators ===

        [Fact]
        public void Increment()
        {
            var v = new EvmUInt256(5);
            v++;
            Assert.Equal(new EvmUInt256(6), v);
        }

        [Fact]
        public void Decrement()
        {
            var v = new EvmUInt256(5);
            v--;
            Assert.Equal(new EvmUInt256(4), v);
        }

        [Fact]
        public void Increment_MaxWraps()
        {
            var v = EvmUInt256.MaxValue;
            v++;
            Assert.Equal(EvmUInt256.Zero, v);
        }

        [Fact]
        public void Decrement_ZeroWraps()
        {
            var v = EvmUInt256.Zero;
            v--;
            Assert.Equal(EvmUInt256.MaxValue, v);
        }

        [Fact]
        public void Min_Max()
        {
            var a = new EvmUInt256(3);
            var b = new EvmUInt256(7);
            Assert.Equal(a, EvmUInt256.Min(a, b));
            Assert.Equal(b, EvmUInt256.Max(a, b));
            Assert.Equal(a, EvmUInt256.Min(a, a));
        }

        [Fact]
        public void Clamp()
        {
            Assert.Equal(new EvmUInt256(5), EvmUInt256.Clamp(new EvmUInt256(5), new EvmUInt256(0), new EvmUInt256(10)));
            Assert.Equal(new EvmUInt256(0), EvmUInt256.Clamp(EvmUInt256.Zero, new EvmUInt256(0), new EvmUInt256(10)));
            Assert.Equal(new EvmUInt256(10), EvmUInt256.Clamp(new EvmUInt256(100), new EvmUInt256(0), new EvmUInt256(10)));
        }

        [Fact]
        public void DivRem_Method()
        {
            EvmUInt256.DivRem(new EvmUInt256(17), new EvmUInt256(5), out var q, out var r);
            Assert.Equal(new EvmUInt256(3), q);
            Assert.Equal(new EvmUInt256(2), r);
        }

        [Fact]
        public void IsPow2_Values()
        {
            Assert.False(EvmUInt256.IsPow2(EvmUInt256.Zero));
            Assert.True(EvmUInt256.IsPow2(EvmUInt256.One));
            Assert.True(EvmUInt256.IsPow2(new EvmUInt256(2)));
            Assert.True(EvmUInt256.IsPow2(new EvmUInt256(256)));
            Assert.False(EvmUInt256.IsPow2(new EvmUInt256(3)));
            Assert.False(EvmUInt256.IsPow2(new EvmUInt256(255)));
            Assert.True(EvmUInt256.IsPow2(EvmUInt256.One << 128));
            Assert.True(EvmUInt256.IsPow2(EvmUInt256.One << 255));
        }

        [Fact]
        public void Log2_Values()
        {
            Assert.Equal(0, EvmUInt256.Log2(EvmUInt256.One));
            Assert.Equal(1, EvmUInt256.Log2(new EvmUInt256(2)));
            Assert.Equal(1, EvmUInt256.Log2(new EvmUInt256(3)));
            Assert.Equal(7, EvmUInt256.Log2(new EvmUInt256(255)));
            Assert.Equal(8, EvmUInt256.Log2(new EvmUInt256(256)));
            Assert.Equal(255, EvmUInt256.Log2(EvmUInt256.MaxValue));
        }

        [Fact]
        public void LeadingZeroCount_Values()
        {
            Assert.Equal(256, EvmUInt256.LeadingZeroCount(EvmUInt256.Zero));
            Assert.Equal(255, EvmUInt256.LeadingZeroCount(EvmUInt256.One));
            Assert.Equal(0, EvmUInt256.LeadingZeroCount(EvmUInt256.MaxValue));
            Assert.Equal(248, EvmUInt256.LeadingZeroCount(new EvmUInt256(255)));
        }

        [Fact]
        public void TrailingZeroCount_Values()
        {
            Assert.Equal(256, EvmUInt256.TrailingZeroCount(EvmUInt256.Zero));
            Assert.Equal(0, EvmUInt256.TrailingZeroCount(EvmUInt256.One));
            Assert.Equal(0, EvmUInt256.TrailingZeroCount(EvmUInt256.MaxValue));
            Assert.Equal(4, EvmUInt256.TrailingZeroCount(new EvmUInt256(16)));
            Assert.Equal(64, EvmUInt256.TrailingZeroCount(new EvmUInt256(0, 0, 1, 0)));
            Assert.Equal(128, EvmUInt256.TrailingZeroCount(new EvmUInt256(0, 1, 0, 0)));
        }

        [Fact]
        public void PopCount_Values()
        {
            Assert.Equal(0, EvmUInt256.PopCount(EvmUInt256.Zero));
            Assert.Equal(1, EvmUInt256.PopCount(EvmUInt256.One));
            Assert.Equal(256, EvmUInt256.PopCount(EvmUInt256.MaxValue));
            Assert.Equal(8, EvmUInt256.PopCount(new EvmUInt256(0xFF)));
            Assert.Equal(2, EvmUInt256.PopCount(new EvmUInt256(0, 1, 0, 1)));
        }

        // === Parse/TryParse ===

        [Fact]
        public void Parse_Decimal()
        {
            Assert.Equal(new EvmUInt256(42), EvmUInt256.Parse("42"));
            Assert.Equal(new EvmUInt256(0), EvmUInt256.Parse("0"));
            Assert.Equal(new EvmUInt256(3000000), EvmUInt256.Parse("3000000"));
        }

        [Fact]
        public void Parse_Hex()
        {
            Assert.Equal(new EvmUInt256(255), EvmUInt256.Parse("0xff"));
            Assert.Equal(new EvmUInt256(256), EvmUInt256.Parse("0x100"));
        }

        [Fact]
        public void Parse_LargeDecimal()
        {
            var maxStr = EvmUInt256.MaxValue.ToDecimalString();
            Assert.Equal(EvmUInt256.MaxValue, EvmUInt256.Parse(maxStr));
        }

        [Fact]
        public void TryParse_Valid()
        {
            Assert.True(EvmUInt256.TryParse("42", out var result));
            Assert.Equal(new EvmUInt256(42), result);
        }

        [Fact]
        public void TryParse_Invalid()
        {
            Assert.False(EvmUInt256.TryParse("", out _));
            Assert.False(EvmUInt256.TryParse(null, out _));
            Assert.False(EvmUInt256.TryParse("abc", out _));
        }

        // === Widening Conversions ===

        [Fact]
        public void ImplicitFromByte()
        {
            EvmUInt256 v = (byte)42;
            Assert.Equal(42UL, v.U0);
            Assert.Equal(0UL, v.U1);
        }

        [Fact]
        public void ImplicitFromUshort()
        {
            EvmUInt256 v = (ushort)1000;
            Assert.Equal(1000UL, v.U0);
        }

        [Fact]
        public void ImplicitFromUint()
        {
            EvmUInt256 v = 42u;
            Assert.Equal(42UL, v.U0);
        }

        // === Narrowing Conversions ===

        [Fact]
        public void ExplicitToUint()
        {
            Assert.Equal(42u, (uint)new EvmUInt256(42));
        }

        [Fact]
        public void ExplicitToUshort()
        {
            Assert.Equal((ushort)42, (ushort)new EvmUInt256(42));
        }

        [Fact]
        public void ExplicitToByte()
        {
            Assert.Equal((byte)42, (byte)new EvmUInt256(42));
        }

        // === HexBigInteger Conversions ===

        [Fact]
        public void HexBigInteger_Roundtrip()
        {
            var v = new EvmUInt256(3000000);
            var hex = v.ToHexBigInteger();
            var rt = hex.ToEvmUInt256();
            Assert.Equal(v, rt);
        }

        [Fact]
        public void HexBigInteger_Roundtrip_Large()
        {
            var v = new EvmUInt256(0xDEAD, 0xBEEF, 0xCAFE, 0xBABE);
            var hex = v.ToHexBigInteger();
            var rt = hex.ToEvmUInt256();
            Assert.Equal(v, rt);
        }

        [Fact]
        public void HexBigInteger_Roundtrip_Zero()
        {
            var v = EvmUInt256.Zero;
            var hex = v.ToHexBigInteger();
            var rt = hex.ToEvmUInt256();
            Assert.Equal(v, rt);
        }

        [Fact]
        public void HexBigInteger_Roundtrip_MaxValue()
        {
            var v = EvmUInt256.MaxValue;
            var hex = v.ToHexBigInteger();
            var rt = hex.ToEvmUInt256();
            Assert.Equal(v, rt);
        }

        [Fact]
        public void HexBigInteger_FromString()
        {
            var hex = new Nethereum.Hex.HexTypes.HexBigInteger("0x2dc6c0"); // 3000000
            var v = hex.ToEvmUInt256();
            Assert.Equal(new EvmUInt256(3000000), v);
        }
    }
}

