using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    /// <summary>
    /// Cross-language test vectors from:
    /// - jsign/binary-tree-spec (Python reference spec, authoritative)
    /// - ethereumjs/ethereumjs-monorepo packages/binarytree
    /// - eth2030/pkg/trie/bintrie (Go reference)
    ///
    /// All spec vectors use BLAKE3 as the hash function.
    /// </summary>
    public class BinaryTrieSpecVectorTests
    {
        private BinaryTrie CreateBlake3Trie()
        {
            return new BinaryTrie(new Blake3HashProvider());
        }

        /// <summary>
        /// Vector from jsign/binary-tree-spec test_tree.py and
        /// ethereumjs binarytree.spec.ts:
        ///   key = 0x00*32, value = 0x01*32
        ///   expected root = 0x694545468677064fd833cddc8455762fe6b21c6cabe2fc172529e0f573181cd5
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void SingleEntry_MatchesSpecRoot()
        {
            var trie = CreateBlake3Trie();
            var key = new byte[32];
            var value = new byte[32];
            for (int i = 0; i < 32; i++) value[i] = 0x01;

            trie.Put(key, value);
            var root = trie.ComputeRoot();
            var expected = "694545468677064fd833cddc8455762fe6b21c6cabe2fc172529e0f573181cd5".HexToByteArray();

            Assert.Equal(expected, root);
        }

        /// <summary>
        /// Vector from jsign/binary-tree-spec and ethereumjs:
        ///   key1 = 0x00*32, value1 = 0x01*32
        ///   key2 = 0x80 0x00*31, value2 = 0x02*32
        ///   expected root = 0x85fc622076752a6fcda2c886c18058d639066a83473d9684704b5a29455ed2ed
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void TwoEntries_DiffFirstBit_MatchesSpecRoot()
        {
            var trie = CreateBlake3Trie();
            var key1 = new byte[32];
            var val1 = new byte[32];
            for (int i = 0; i < 32; i++) val1[i] = 0x01;

            var key2 = new byte[32];
            key2[0] = 0x80;
            var val2 = new byte[32];
            for (int i = 0; i < 32; i++) val2[i] = 0x02;

            trie.Put(key1, val1);
            trie.Put(key2, val2);
            var root = trie.ComputeRoot();
            var expected = "85fc622076752a6fcda2c886c18058d639066a83473d9684704b5a29455ed2ed".HexToByteArray();

            Assert.Equal(expected, root);
        }

        /// <summary>
        /// Vector from jsign/binary-tree-spec test_tree.py:
        ///   4 keys: 0x00*32, 0x80+0x00*31, 0x01+0x00*31, 0x81+0x00*31
        ///   values: 1, 2, 3, 4 as LE uint64 padded to 32 bytes
        ///   expected root = 0xe93c209026b8b00d76062638102ece415028bd104e1d892d5399375a323f2218
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void FourEntries_MatchesSpecRoot()
        {
            var trie = CreateBlake3Trie();

            var keys = new byte[][]
            {
                new byte[32],                                      // 0x00*32
                MakeKey(0x80),                                     // 0x80 0x00*31
                MakeKey(0x01),                                     // 0x01 0x00*31
                MakeKey(0x81),                                     // 0x81 0x00*31
            };

            for (int i = 0; i < 4; i++)
            {
                var val = new byte[32];
                val[0] = (byte)(i + 1); // LE uint64: 1, 2, 3, 4
                trie.Put(keys[i], val);
            }

            var root = trie.ComputeRoot();
            var expected = "e93c209026b8b00d76062638102ece415028bd104e1d892d5399375a323f2218".HexToByteArray();

            Assert.Equal(expected, root);
        }

        /// <summary>
        /// Verify that empty trie always returns zero hash regardless of hash function.
        /// Matches Go, Python, and ethereumjs implementations.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void EmptyTrie_ZeroRoot_Blake3()
        {
            var trie = CreateBlake3Trie();
            Assert.Equal(new byte[32], trie.ComputeRoot());
        }

        /// <summary>
        /// Verify insertion order does not affect root hash.
        /// This is a fundamental trie invariant tested across all reference implementations.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void InsertionOrder_DoesNotAffectRoot_Blake3()
        {
            var key1 = new byte[32];
            var key2 = MakeKey(0x80);
            var key3 = MakeKey(0x01);
            var val = new byte[32]; val[0] = 0xFF;

            var trie1 = CreateBlake3Trie();
            trie1.Put(key1, val);
            trie1.Put(key2, val);
            trie1.Put(key3, val);

            var trie2 = CreateBlake3Trie();
            trie2.Put(key3, val);
            trie2.Put(key1, val);
            trie2.Put(key2, val);

            var trie3 = CreateBlake3Trie();
            trie3.Put(key2, val);
            trie3.Put(key3, val);
            trie3.Put(key1, val);

            Assert.Equal(trie1.ComputeRoot(), trie2.ComputeRoot());
            Assert.Equal(trie1.ComputeRoot(), trie3.ComputeRoot());
        }

        /// <summary>
        /// Colocated values (same stem, different suffix) should share one StemNode.
        /// Matches Go TestOneStemColocatedValues.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void ColocatedValues_SingleStemNode_Blake3()
        {
            var trie = CreateBlake3Trie();
            var val = new byte[32]; val[0] = 0x01;

            byte[] suffixes = { 0x03, 0x04, 0x09, 0xFF };
            foreach (var s in suffixes)
            {
                var key = new byte[32];
                key[31] = s;
                trie.Put(key, val);
            }

            Assert.Equal(1, trie.GetHeight());
        }

        /// <summary>
        /// Two stems that differ at first bit produce height 2.
        /// Matches Go TestTwoStemColocatedValues.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void TwoStems_DiffFirstBit_Height2_Blake3()
        {
            var trie = CreateBlake3Trie();
            var val = new byte[32]; val[0] = 0x01;

            trie.Put(MakeKeyWithSuffix(0x00, 0x03), val);
            trie.Put(MakeKeyWithSuffix(0x00, 0x04), val);
            trie.Put(MakeKeyWithSuffix(0x80, 0x03), val);
            trie.Put(MakeKeyWithSuffix(0x80, 0x04), val);

            Assert.Equal(2, trie.GetHeight());
        }

        /// <summary>
        /// Two keys matching first 42 bits should produce height 1+42+1.
        /// Matches Go TestTwoKeysMatchFirst42Bits.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void TwoKeysShared42Bits_ProperHeight_Blake3()
        {
            var trie = CreateBlake3Trie();
            var val1 = new byte[32]; val1[0] = 0x01;
            var val2 = new byte[32]; val2[0] = 0x02;

            // key1: 0000000000C0...
            var key1 = "0000000000C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0C0".HexToByteArray();
            // key2: 0000000000E0...
            var key2 = "0000000000E00000000000000000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(key1, val1);
            trie.Put(key2, val2);

            Assert.Equal(1 + 42 + 1, trie.GetHeight());
        }

        /// <summary>
        /// 256 entries with different first bytes should produce height 1+8.
        /// Matches Go TestLargeNumberOfEntries.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void Entries256_Height9_Blake3()
        {
            var trie = CreateBlake3Trie();
            var val = new byte[32]; val[0] = 0xFF;

            for (int i = 0; i < 256; i++)
            {
                var key = new byte[32];
                key[0] = (byte)i;
                trie.Put(key, val);
            }

            Assert.Equal(1 + 8, trie.GetHeight());
        }

        /// <summary>
        /// Put then delete, then re-verify root changes.
        /// Matches Go TestBinaryTrieGetPutDelete.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void PutGetDelete_RoundTrip_Blake3()
        {
            var trie = CreateBlake3Trie();
            var key = new byte[32]; key[31] = 0x01;
            var val = "deadbeef00000000000000000000000000000000000000000000000000000000".HexToByteArray();

            trie.Put(key, val);
            var got = trie.Get(key);
            Assert.Equal(val, got);

            var hashAfterPut = trie.ComputeRoot();
            Assert.NotEqual(new byte[32], hashAfterPut);

            trie.Delete(key);
            got = trie.Get(key);
            Assert.Equal(new byte[32], got);
        }

        /// <summary>
        /// Deep copy produces identical root; modifying copy diverges.
        /// Matches Go TestBinaryTrieCopy.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void Copy_IndependentMutation_Blake3()
        {
            var trie = CreateBlake3Trie();
            var key = new byte[32]; key[31] = 0x01;
            var val = "deadbeef00000000000000000000000000000000000000000000000000000000".HexToByteArray();
            trie.Put(key, val);

            var copy = trie.Copy();
            Assert.Equal(trie.ComputeRoot(), copy.ComputeRoot());

            var key2 = new byte[32]; key2[0] = 0x80; key2[31] = 0x01;
            copy.Put(key2, val);
            Assert.NotEqual(trie.ComputeRoot(), copy.ComputeRoot());
        }

        /// <summary>
        /// Duplicate key update replaces value; height stays the same.
        /// Matches Go TestInsertDuplicateKey.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void DuplicateKey_UpdatesValue_Blake3()
        {
            var trie = CreateBlake3Trie();
            var key = new byte[32]; for (int i = 0; i < 32; i++) key[i] = 0x01;

            var val1 = new byte[32]; for (int i = 0; i < 32; i++) val1[i] = 0x01;
            var val2 = new byte[32]; for (int i = 0; i < 32; i++) val2[i] = 0x02;

            trie.Put(key, val1);
            trie.Put(key, val2);

            Assert.Equal(1, trie.GetHeight());
            Assert.Equal(val2, trie.Get(key));
        }

        /// <summary>
        /// 100 sequential puts followed by 100 gets.
        /// Matches Go TestBinaryTrieMultiplePuts.
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void MultiplePuts100_AllRetrievable_Blake3()
        {
            var trie = CreateBlake3Trie();
            int n = 100;
            for (int i = 0; i < n; i++)
            {
                var key = new byte[32];
                key[24] = (byte)((i >> 24) & 0xFF);
                key[25] = (byte)((i >> 16) & 0xFF);
                key[26] = (byte)((i >> 8) & 0xFF);
                key[27] = (byte)(i & 0xFF);
                var val = new byte[32];
                int v = i + 1000;
                val[24] = (byte)((v >> 24) & 0xFF);
                val[25] = (byte)((v >> 16) & 0xFF);
                val[26] = (byte)((v >> 8) & 0xFF);
                val[27] = (byte)(v & 0xFF);
                trie.Put(key, val);
            }

            for (int i = 0; i < n; i++)
            {
                var key = new byte[32];
                key[24] = (byte)((i >> 24) & 0xFF);
                key[25] = (byte)((i >> 16) & 0xFF);
                key[26] = (byte)((i >> 8) & 0xFF);
                key[27] = (byte)(i & 0xFF);
                var expected = new byte[32];
                int v = i + 1000;
                expected[24] = (byte)((v >> 24) & 0xFF);
                expected[25] = (byte)((v >> 16) & 0xFF);
                expected[26] = (byte)((v >> 8) & 0xFF);
                expected[27] = (byte)(v & 0xFF);

                Assert.Equal(expected, trie.Get(key));
            }
        }

        /// <summary>
        /// Verify StemNode hash computation matches the spec formula:
        ///   hash(stem_padded_32 || merkle_root_of_values)
        /// where stem_padded_32 = stem(31) || 0x00
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void StemNodeHash_MatchesSpecFormula()
        {
            var blake3 = new Blake3HashProvider();

            var stem = new byte[31];
            var values = new byte[256][];
            values[0] = new byte[32];
            for (int i = 0; i < 32; i++) values[0][i] = 0x01;

            var node = new StemBinaryNode(stem, values, 0);
            var nodeHash = node.ComputeHash(blake3);

            var valueHash = blake3.ComputeHash(values[0]);
            var merkleData = new byte[256][];
            merkleData[0] = valueHash;
            for (int level = 1; level <= 8; level++)
            {
                int count = 256 / (1 << level);
                for (int i = 0; i < count; i++)
                {
                    var left = merkleData[i * 2];
                    var right = merkleData[i * 2 + 1];
                    if (left == null && right == null)
                    {
                        merkleData[i] = null;
                        continue;
                    }
                    var pair = new byte[64];
                    if (left != null) Array.Copy(left, 0, pair, 0, 32);
                    if (right != null) Array.Copy(right, 0, pair, 32, 32);
                    merkleData[i] = blake3.ComputeHash(pair);
                }
            }
            var valuesRoot = merkleData[0] ?? new byte[32];

            // stem(31) || 0x00 || merkle_root(32) = 64 bytes
            var preimage = new byte[64];
            Array.Copy(stem, 0, preimage, 0, 31);
            preimage[31] = 0x00;
            Array.Copy(valuesRoot, 0, preimage, 32, 32);
            var expectedHash = blake3.ComputeHash(preimage);

            Assert.Equal(expectedHash, nodeHash);
        }

        /// <summary>
        /// Verify InternalNode hash matches spec formula:
        ///   hash(left_hash || right_hash)
        /// </summary>
        [Fact]
        [Trait("Category", "BinaryTrie-SpecVectors")]
        public void InternalNodeHash_MatchesSpecFormula()
        {
            var blake3 = new Blake3HashProvider();

            var stem1 = new byte[31];
            var values1 = new byte[256][];
            values1[0] = new byte[32]; values1[0][0] = 0xAA;

            var stem2 = new byte[31]; stem2[0] = 0x80;
            var values2 = new byte[256][];
            values2[0] = new byte[32]; values2[0][0] = 0xBB;

            var left = new StemBinaryNode(stem1, values1, 1);
            var right = new StemBinaryNode(stem2, values2, 1);
            var node = new InternalBinaryNode(0, left, right);

            var nodeHash = node.ComputeHash(blake3);

            var leftHash = left.ComputeHash(blake3);
            var rightHash = right.ComputeHash(blake3);
            var pair = new byte[64];
            Array.Copy(leftHash, 0, pair, 0, 32);
            Array.Copy(rightHash, 0, pair, 32, 32);
            var expected = blake3.ComputeHash(pair);

            Assert.Equal(expected, nodeHash);
        }

        private static byte[] MakeKey(byte firstByte)
        {
            var key = new byte[32];
            key[0] = firstByte;
            return key;
        }

        private static byte[] MakeKeyWithSuffix(byte firstByte, byte suffix)
        {
            var key = new byte[32];
            key[0] = firstByte;
            key[31] = suffix;
            return key;
        }
    }
}
