using System.Numerics;
using Nethereum.Merkle.Binary.Keys;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class BasicDataLeafTests
    {
        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void PackUnpack_RoundTrip()
        {
            byte version = 1;
            uint codeSize = 100;
            ulong nonce = 5;
            var balance = BigInteger.Pow(10, 18);

            var packed = BasicDataLeaf.Pack(version, codeSize, nonce, balance);
            BasicDataLeaf.Unpack(packed, out var v, out var cs, out var n, out var b);

            Assert.Equal(version, v);
            Assert.Equal(codeSize, cs);
            Assert.Equal(nonce, n);
            Assert.Equal(balance, b);
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_ByteOffsets()
        {
            var packed = BasicDataLeaf.Pack(0xFF, 0x00AABBCC, 0x0102030405060708, BigInteger.Zero);

            Assert.Equal(0xFF, packed[0]);
            for (int i = 1; i <= 4; i++)
                Assert.Equal(0, packed[i]);
            Assert.Equal(0xAA, packed[5]);
            Assert.Equal(0xBB, packed[6]);
            Assert.Equal(0xCC, packed[7]);
            Assert.Equal(0x01, packed[8]);
            Assert.Equal(0x02, packed[9]);
            Assert.Equal(0x03, packed[10]);
            Assert.Equal(0x04, packed[11]);
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_ZeroValues_AllZeros()
        {
            var packed = BasicDataLeaf.Pack(0, 0, 0, BigInteger.Zero);
            for (int i = 0; i < 32; i++)
                Assert.Equal(0, packed[i]);
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_MaxBalance_RoundTrip()
        {
            var maxBalance = BigInteger.Pow(2, 128) - 1;
            var packed = BasicDataLeaf.Pack(0, 0, 0, maxBalance);
            BasicDataLeaf.Unpack(packed, out _, out _, out _, out var balance);
            Assert.Equal(maxBalance, balance);
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_Returns32Bytes()
        {
            var packed = BasicDataLeaf.Pack(1, 42, 100, new BigInteger(1000000));
            Assert.Equal(32, packed.Length);
        }
    }
}
