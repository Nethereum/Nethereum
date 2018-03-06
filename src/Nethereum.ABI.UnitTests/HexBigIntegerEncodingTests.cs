using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class HexBigIntegerEncodingTests
    {
        [Fact]
        public virtual void EncodingDecodingRandom10000Test()
        {
            var random = new Random();

            for (var i = 0; i < 10000; i++)
            {
                var number = random.Next(0, 100000);
                var value = i * number + "000000000000";
                var encode = new HexBigInteger(BigInteger.Parse(value));
                Assert.Equal(encode.Value.ToString(), new HexBigInteger(encode.HexValue).Value.ToString());
            }
        }

        [Fact]
        public virtual void ShouldDecode08ac7230489e80000()
        {
            var encode = new HexBigInteger(BigInteger.Parse("10000000000000000000"));
            var x = new HexBigInteger("0x8ac7230489e80000");
            Assert.Equal(encode.Value.ToString(), x.Value.ToString());
        }

        [Fact]
        public virtual void ShouldDecode0x0()
        {
            var x = new HexBigInteger("0x0");
            Assert.Equal(0, x.Value);
        }

        [Fact]
        public virtual void ShouldDecode1000000000000000000()
        {
            var encode = new HexBigInteger(BigInteger.Parse("1000000000000000000"));
            var x = new HexBigInteger("0xde0b6b3a7640000");
            Assert.Equal(encode.Value.ToString(), x.Value.ToString());
        }

        [Fact]
        public virtual void ShouldDecode10000000000000000000()
        {
            var encode = new HexBigInteger(BigInteger.Parse("10000000000000000000"));
            var x = new HexBigInteger("0x008ac7230489e80000");
            Assert.Equal(encode.Value.ToString(), x.Value.ToString());
        }

        [Fact]
        public virtual void ShouldDecodeCompactNoTraillingZeros()
        {
            var x = new HexBigInteger("0x400");
            Assert.Equal(1024, x.Value);
        }

        /*
        
        When encoding QUANTITIES (integers, numbers): encode as hex, prefix with "0x", the most compact representation (slight exception: zero should be represented as "0x0"). Examples:

        0x41 (65 in decimal)
        0x400 (1024 in decimal)
        WRONG: 0x (should always have at least one digit - zero is "0x0")
        WRONG: 0x0400 (no leading zeroes allowed)
        WRONG: ff (must be prefixed 0x)
        
        */
        [Fact]
        public virtual void ShouldEncode0as0x0()
        {
            var x = new HexBigInteger(new BigInteger(0));
            Assert.Equal("0x0", x.HexValue);
        }


        [Fact]
        public virtual void ShouldEncodeCompactNoTraillingZeros()
        {
            var x = new HexBigInteger(new BigInteger(1024));
            Assert.Equal("0x400", x.HexValue); // not "0x0400"
        }


        [Fact]
        public void HexBigIntergerTest()
        {
            HexBigInteger TestValue = new HexBigInteger("0x100");

            Assert.Equal(TestValue.HexValue, "0x100");
            Assert.Equal(TestValue.Value, 256);

            TestValue.Value = 1024;
            Assert.Equal(TestValue.Value, 1024);      

            TestValue.HexValue = "0x200";            
            Assert.Equal(TestValue.HexValue, "0x200");
        }
    }
}