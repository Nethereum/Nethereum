using System;
using System.Numerics;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class EvmUInt256Tests
    {
        [Fact]
        public void Add_BasicArithmetic()
        {
            var a = new EvmUInt256(3);
            var b = new EvmUInt256(5);
            Assert.Equal(new EvmUInt256(8), a + b);
        }

        [Fact]
        public void Add_Overflow_Wraps()
        {
            var max = EvmUInt256.MaxValue;
            var one = EvmUInt256.One;
            Assert.Equal(EvmUInt256.Zero, max + one);
        }

        [Fact]
        public void Sub_BasicArithmetic()
        {
            var a = new EvmUInt256(10);
            var b = new EvmUInt256(3);
            Assert.Equal(new EvmUInt256(7), a - b);
        }

        [Fact]
        public void Sub_Underflow_Wraps()
        {
            var zero = EvmUInt256.Zero;
            var one = EvmUInt256.One;
            Assert.Equal(EvmUInt256.MaxValue, zero - one);
        }

        [Fact]
        public void Mul_BasicArithmetic()
        {
            var a = new EvmUInt256(6);
            var b = new EvmUInt256(7);
            Assert.Equal(new EvmUInt256(42), a * b);
        }

        [Fact]
        public void Div_BasicArithmetic()
        {
            var a = new EvmUInt256(42);
            var b = new EvmUInt256(6);
            Assert.Equal(new EvmUInt256(7), a / b);
        }

        [Fact]
        public void Div_ByZero_ReturnsZero()
        {
            var a = new EvmUInt256(42);
            Assert.Equal(EvmUInt256.Zero, a / EvmUInt256.Zero);
        }

        [Fact]
        public void Mod_BasicArithmetic()
        {
            var a = new EvmUInt256(10);
            var b = new EvmUInt256(3);
            Assert.Equal(new EvmUInt256(1), a % b);
        }

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
        public void Bitwise_And()
        {
            var a = new EvmUInt256(0xFF);
            var b = new EvmUInt256(0x0F);
            Assert.Equal(new EvmUInt256(0x0F), a & b);
        }

        [Fact]
        public void Bitwise_Or()
        {
            var a = new EvmUInt256(0xF0);
            var b = new EvmUInt256(0x0F);
            Assert.Equal(new EvmUInt256(0xFF), a | b);
        }

        [Fact]
        public void Bitwise_Xor()
        {
            var a = new EvmUInt256(0xFF);
            var b = new EvmUInt256(0x0F);
            Assert.Equal(new EvmUInt256(0xF0), a ^ b);
        }

        [Fact]
        public void Bitwise_Not()
        {
            Assert.Equal(EvmUInt256.MaxValue, ~EvmUInt256.Zero);
            Assert.Equal(EvmUInt256.Zero, ~EvmUInt256.MaxValue);
        }

        [Fact]
        public void ShiftLeft()
        {
            var one = EvmUInt256.One;
            Assert.Equal(new EvmUInt256(16), one << 4);
            Assert.Equal(new EvmUInt256(0, 0, 1, 0), one << 64);
            Assert.Equal(new EvmUInt256(0, 1, 0, 0), one << 128);
        }

        [Fact]
        public void ShiftRight()
        {
            var val = new EvmUInt256(16);
            Assert.Equal(new EvmUInt256(4), val >> 2);
        }

        [Fact]
        public void ByteArrayRoundtrip()
        {
            var val = new EvmUInt256(0xDEAD, 0xBEEF, 0xCAFE, 0xBABE);
            var bytes = val.ToBigEndian();
            var restored = EvmUInt256.FromBigEndian(bytes);
            Assert.Equal(val, restored);
        }

        [Fact]
        public void ByteArrayRoundtrip_MaxValue()
        {
            var bytes = EvmUInt256.MaxValue.ToBigEndian();
            Assert.Equal(32, bytes.Length);
            for (int i = 0; i < 32; i++)
                Assert.Equal(0xFF, bytes[i]);
            Assert.Equal(EvmUInt256.MaxValue, EvmUInt256.FromBigEndian(bytes));
        }

        [Fact]
        public void ByteArrayRoundtrip_Small()
        {
            var val = new EvmUInt256(0x42);
            var bytes = val.ToBigEndian();
            Assert.Equal(0x42, bytes[31]);
            Assert.Equal(0, bytes[0]);
        }

        [Fact]
        public void GetByte_MostSignificant()
        {
            var val = EvmUInt256.FromBigEndian(new byte[] { 0x42 });
            Assert.Equal(0x42, val.GetByte(31));
            Assert.Equal(0, val.GetByte(0));
        }

        [Fact]
        public void BigIntegerRoundtrip()
        {
            var bi = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
            var val = EvmUInt256BigIntegerExtensions.FromBigInteger(bi);
            Assert.Equal(EvmUInt256.MaxValue, val);
            Assert.Equal(bi, val.ToBigInteger());
        }

        [Fact]
        public void Negate_TwosComplement()
        {
            var one = EvmUInt256.One;
            var negOne = one.Negate();
            Assert.Equal(EvmUInt256.MaxValue, negOne);
            Assert.Equal(EvmUInt256.One, negOne.Negate());
        }

        [Fact]
        public void IsZero_Checks()
        {
            Assert.True(EvmUInt256.Zero.IsZero);
            Assert.False(EvmUInt256.One.IsZero);
            Assert.False(EvmUInt256.MaxValue.IsZero);
        }

        [Fact]
        public void Mul_LargeNumbers()
        {
            // 2^128 * 2^128 = 2^256 = 0 (overflow)
            var a = EvmUInt256.One << 128;
            var b = EvmUInt256.One << 128;
            Assert.Equal(EvmUInt256.Zero, a * b);
        }

        [Fact]
        public void Mul_CrossLimb()
        {
            // (2^64 - 1) * 2 should be correct
            var a = new EvmUInt256(ulong.MaxValue);
            var two = new EvmUInt256(2);
            var result = a * two;
            Assert.Equal(new EvmUInt256(0, 0, 1, ulong.MaxValue - 1), result);
        }
        [Fact]
        public void AddMod_Simple()
        {
            Assert.Equal(new EvmUInt256(0), EvmUInt256.AddMod(new EvmUInt256(3), new EvmUInt256(7), new EvmUInt256(5)));
            Assert.Equal(new EvmUInt256(1), EvmUInt256.AddMod(new EvmUInt256(10), new EvmUInt256(20), new EvmUInt256(29)));
            Assert.Equal(EvmUInt256.Zero, EvmUInt256.AddMod(new EvmUInt256(5), new EvmUInt256(3), EvmUInt256.Zero));
        }

        [Fact]
        public void AddMod_Overflow()
        {
            // MaxValue + 1 mod 3 = (2^256 - 1 + 1) % 3 = 2^256 % 3
            var bi = (System.Numerics.BigInteger.Pow(2, 256) - 1 + 1) % 3;
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
            // MaxValue * MaxValue mod 7 — compare with BigInteger
            var maxBi = System.Numerics.BigInteger.Pow(2, 256) - 1;
            var expected = (maxBi * maxBi) % 7;
            var result = EvmUInt256.MulMod(EvmUInt256.MaxValue, EvmUInt256.MaxValue, new EvmUInt256(7));
            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(expected), result);
        }

        [Fact]
        public void BigMul_Simple()
        {
            var upper = EvmUInt256.BigMul(new EvmUInt256(3), new EvmUInt256(7), out var lower);
            Assert.Equal(EvmUInt256.Zero, upper);
            Assert.Equal(new EvmUInt256(21), lower);
        }

        [Fact]
        public void BigMul_Overflow()
        {
            // MaxValue * 2 = 2^257 - 2 = upper=1, lower=MaxValue-1
            var upper = EvmUInt256.BigMul(EvmUInt256.MaxValue, new EvmUInt256(2), out var lower);
            Assert.Equal(EvmUInt256.One, upper);
            Assert.Equal(EvmUInt256.MaxValue - EvmUInt256.One, lower);
        }

        [Fact]
        public void ModPow_Simple()
        {
            Assert.Equal(new EvmUInt256(24), EvmUInt256.ModPow(new EvmUInt256(2), new EvmUInt256(10), new EvmUInt256(1000)));
        }

        [Fact]
        public void ModPow_MatchesBigInteger()
        {
            // 7^123 mod 1000000007
            var expected = System.Numerics.BigInteger.ModPow(7, 123, 1000000007);
            var result = EvmUInt256.ModPow(new EvmUInt256(7), new EvmUInt256(123), new EvmUInt256(1000000007));
            Assert.Equal(EvmUInt256BigIntegerExtensions.FromBigInteger(expected), result);
        }

        [Fact]
        public void Exp_MatchesBigInteger()
        {
            var two256 = System.Numerics.BigInteger.Pow(2, 256);
            // Test several EXP cases against BigInteger
            void CheckExp(ulong baseVal, ulong exp)
            {
                var expected = System.Numerics.BigInteger.ModPow(baseVal, exp, two256);
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
            CheckExp(2, 10);      // 1024
            CheckExp(2, 255);     // large
            CheckExp(3, 100);     // large
            CheckExp(256, 1);     // simple
            CheckExp(0, 0);       // edge: 0^0 = 1
            CheckExp(1, 1000);    // 1
        }

        [Fact]
        public void Mul_MatchesBigInteger_Fuzz()
        {
            var two256 = System.Numerics.BigInteger.Pow(2, 256);
            var rng = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                var aBytes = new byte[32];
                var bBytes = new byte[32];
                rng.NextBytes(aBytes);
                rng.NextBytes(bBytes);
                var aBi = new System.Numerics.BigInteger(aBytes, isUnsigned: true, isBigEndian: true);
                var bBi = new System.Numerics.BigInteger(bBytes, isUnsigned: true, isBigEndian: true);
                var expectedBi = (aBi * bBi) % two256;
                var a = EvmUInt256.FromBigEndian(aBytes);
                var b = EvmUInt256.FromBigEndian(bBytes);
                var result = a * b;
                var resultBi = result.ToBigInteger();
                Assert.Equal(expectedBi, resultBi);
            }
        }

        [Fact]
        public void ToDecimalString_Values()
        {
            Assert.Equal("0", EvmUInt256.Zero.ToDecimalString());
            Assert.Equal("1", EvmUInt256.One.ToDecimalString());
            Assert.Equal("42", new EvmUInt256(42).ToDecimalString());
            Assert.Equal("18446744073709551615", new EvmUInt256(ulong.MaxValue).ToDecimalString());
        }
    }
}
