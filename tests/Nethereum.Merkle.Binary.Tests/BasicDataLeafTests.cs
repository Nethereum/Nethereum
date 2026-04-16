using System;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Util;
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
            var balance = (EvmUInt256)1_000_000_000_000_000_000UL; // 1 ETH in wei

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
            var packed = BasicDataLeaf.Pack(0xFF, 0x00AABBCC, 0x0102030405060708, EvmUInt256.Zero);

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
            var packed = BasicDataLeaf.Pack(0, 0, 0, EvmUInt256.Zero);
            for (int i = 0; i < 32; i++)
                Assert.Equal(0, packed[i]);
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_MaxBalance_RoundTrip()
        {
            // 2^128 - 1: lower two ulongs set, upper two zero
            var maxBalance = new EvmUInt256(0UL, 0UL, ulong.MaxValue, ulong.MaxValue);
            var packed = BasicDataLeaf.Pack(0, 0, 0, maxBalance);
            BasicDataLeaf.Unpack(packed, out _, out _, out _, out var balance);
            Assert.Equal(maxBalance, balance);
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_BalanceOverflow_Throws()
        {
            // 2^128 overflows the 128-bit leaf field (u2 bit 0 set)
            var overflowBalance = new EvmUInt256(0UL, 1UL, 0UL, 0UL);
            Assert.Throws<ArgumentOutOfRangeException>(() => BasicDataLeaf.Pack(0, 0, 0, overflowBalance));
        }

        [Fact]
        [Trait("Category", "BasicDataLeaf")]
        public void Pack_Returns32Bytes()
        {
            var packed = BasicDataLeaf.Pack(1, 42, 100, (EvmUInt256)1000000UL);
            Assert.Equal(32, packed.Length);
        }
    }
}
