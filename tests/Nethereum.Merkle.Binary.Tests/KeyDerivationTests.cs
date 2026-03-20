using System;
using System.Numerics;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class KeyDerivationTests
    {
        private readonly BinaryTreeKeyDerivation _keyDerivation =
            new BinaryTreeKeyDerivation(new Blake3HashProvider());

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKey_SubIndexSetCorrectly()
        {
            var addr = new byte[20]; addr[0] = 0xAA;
            var addr32 = BinaryTreeKeyDerivation.AddressTo32(addr);
            var key = _keyDerivation.GetTreeKey(addr32, BigInteger.Zero, 128);
            Assert.Equal(128, key[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKey_DifferentTreeIndex_DifferentStem()
        {
            var addr = new byte[20]; addr[0] = 0x11;
            var addr32 = BinaryTreeKeyDerivation.AddressTo32(addr);
            var k0 = _keyDerivation.GetTreeKey(addr32, BigInteger.Zero, 0);
            var k1 = _keyDerivation.GetTreeKey(addr32, BigInteger.One, 0);

            bool differs = false;
            for (int i = 0; i < 31; i++)
                if (k0[i] != k1[i]) { differs = true; break; }
            Assert.True(differs);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForBasicData_CorrectSubIndex()
        {
            var addr = new byte[20]; addr[0] = 0x42;
            var key = _keyDerivation.GetTreeKeyForBasicData(addr);
            Assert.Equal(BinaryTreeKeyDerivation.BasicDataLeafKey, key[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForBasicData_MatchesGetTreeKey()
        {
            var addr = new byte[20]; addr[0] = 0x42;
            var key = _keyDerivation.GetTreeKeyForBasicData(addr);
            var expected = _keyDerivation.GetTreeKey(
                BinaryTreeKeyDerivation.AddressTo32(addr), BigInteger.Zero, BinaryTreeKeyDerivation.BasicDataLeafKey);
            Assert.Equal(expected, key);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForCodeHash_CorrectSubIndex()
        {
            var addr = new byte[20]; addr[0] = 0x42;
            var key = _keyDerivation.GetTreeKeyForCodeHash(addr);
            Assert.Equal(BinaryTreeKeyDerivation.CodeHashLeafKey, key[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForCodeChunk_ChunkZero_SubIndexIsCodeOffset()
        {
            var addr = new byte[20]; addr[0] = 0x01;
            var key = _keyDerivation.GetTreeKeyForCodeChunk(addr, 0);
            Assert.Equal(BinaryTreeKeyDerivation.CodeOffset, key[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForCodeChunk_Chunk128_DifferentStem()
        {
            var addr = new byte[20]; addr[0] = 0x01;
            var k0 = _keyDerivation.GetTreeKeyForCodeChunk(addr, 0);
            var k128 = _keyDerivation.GetTreeKeyForCodeChunk(addr, 128);

            bool stemsDiffer = false;
            for (int i = 0; i < 31; i++)
                if (k0[i] != k128[i]) { stemsDiffer = true; break; }
            Assert.True(stemsDiffer);
            Assert.Equal(0, k128[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForStorageSlot_InlineSlot0()
        {
            var addr = new byte[20]; addr[0] = 0x11;
            var key = _keyDerivation.GetTreeKeyForStorageSlot(addr, BigInteger.Zero);
            Assert.Equal(BinaryTreeKeyDerivation.HeaderStorageOffset, key[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForStorageSlot_InlineSlot63()
        {
            var addr = new byte[20]; addr[0] = 0x11;
            var key = _keyDerivation.GetTreeKeyForStorageSlot(addr, new BigInteger(63));
            Assert.Equal(127, key[31]);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKeyForStorageSlot_MainStorage64_DifferentStem()
        {
            var addr = new byte[20]; addr[0] = 0x11;
            var key63 = _keyDerivation.GetTreeKeyForStorageSlot(addr, new BigInteger(63));
            var key64 = _keyDerivation.GetTreeKeyForStorageSlot(addr, new BigInteger(64));

            bool stemsDiffer = false;
            for (int i = 0; i < 31; i++)
                if (key63[i] != key64[i]) { stemsDiffer = true; break; }
            Assert.True(stemsDiffer);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void GetTreeKey_200StorageSlots_AllUnique()
        {
            var addr = new byte[20]; addr[0] = 0xAB; addr[1] = 0xCD;
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                var key = _keyDerivation.GetTreeKeyForStorageSlot(addr, new BigInteger(i));
                var hex = BitConverter.ToString(key);
                Assert.DoesNotContain(hex, seen);
                seen.Add(hex);
            }
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void BasicDataKey_Distinct_From_StorageSlot0Key()
        {
            var addr = new byte[20]; addr[0] = 0x77;
            var basicKey = _keyDerivation.GetTreeKeyForBasicData(addr);
            var storageKey = _keyDerivation.GetTreeKeyForStorageSlot(addr, BigInteger.Zero);
            Assert.NotEqual(basicKey, storageKey);
        }

        [Fact]
        [Trait("Category", "KeyDerivation")]
        public void AddressTo32_PadsCorrectly()
        {
            var addr = new byte[] { 0xAA, 0xBB, 0xCC };
            var addr32 = BinaryTreeKeyDerivation.AddressTo32(addr);
            Assert.Equal(32, addr32.Length);
            Assert.Equal(0x00, addr32[0]);
            Assert.Equal(0xAA, addr32[29]);
            Assert.Equal(0xBB, addr32[30]);
            Assert.Equal(0xCC, addr32[31]);
        }
    }
}
