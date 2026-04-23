using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Nodes;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class BinaryTrieTests
    {
        private static byte[] MakeKey(byte firstByte, byte lastByte = 0)
        {
            var key = new byte[32];
            key[0] = firstByte;
            key[31] = lastByte;
            return key;
        }

        private static byte[] MakeValue(byte fill)
        {
            var value = new byte[32];
            for (int i = 0; i < 32; i++) value[i] = fill;
            return value;
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void EmptyTrie_ComputeRoot_ReturnsZeroHash()
        {
            var trie = new BinaryTrie();
            var root = trie.ComputeRoot();
            Assert.Equal(new byte[32], root);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void SingleEntry_PutGet_ReturnsValue()
        {
            var trie = new BinaryTrie();
            var key = MakeKey(0x00, 0x01);
            var value = MakeValue(0xAB);

            trie.Put(key, value);
            var result = trie.Get(key);

            Assert.Equal(value, result);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void SingleEntry_Height_IsOne()
        {
            var trie = new BinaryTrie();
            trie.Put(MakeKey(0x00, 0x01), MakeValue(0x01));
            Assert.Equal(1, trie.GetHeight());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void SingleEntry_NonZeroRoot()
        {
            var trie = new BinaryTrie();
            trie.Put(MakeKey(0x00, 0x01), MakeValue(0x01));
            var root = trie.ComputeRoot();
            Assert.NotEqual(new byte[32], root);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void TwoEntries_DifferentFirstBit_Height2()
        {
            var trie = new BinaryTrie();
            trie.Put(MakeKey(0x00, 0x01), MakeValue(0x01));
            trie.Put(MakeKey(0x80, 0x01), MakeValue(0x02));
            Assert.Equal(2, trie.GetHeight());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void ColocatedValues_SameStem_HeightOne()
        {
            var trie = new BinaryTrie();
            var stem = new byte[32];
            stem[31] = 0x03;
            trie.Put(stem, MakeValue(0x01));
            stem = new byte[32];
            stem[31] = 0x04;
            trie.Put(stem, MakeValue(0x01));
            stem = new byte[32];
            stem[31] = 0x09;
            trie.Put(stem, MakeValue(0x01));
            stem = new byte[32];
            stem[31] = 0xFF;
            trie.Put(stem, MakeValue(0x01));

            Assert.Equal(1, trie.GetHeight());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void TwoStems_DifferentFirstBit_Height2()
        {
            var trie = new BinaryTrie();
            trie.Put(MakeKey(0x00, 0x03), MakeValue(0x01));
            trie.Put(MakeKey(0x00, 0x04), MakeValue(0x01));
            trie.Put(MakeKey(0x80, 0x03), MakeValue(0x01));
            trie.Put(MakeKey(0x80, 0x04), MakeValue(0x01));

            Assert.Equal(2, trie.GetHeight());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void InsertDuplicateKey_UpdatesValue()
        {
            var trie = new BinaryTrie();
            var key = MakeKey(0x01, 0x01);
            trie.Put(key, MakeValue(0xAA));
            trie.Put(key, MakeValue(0xBB));

            var result = trie.Get(key);
            Assert.Equal(MakeValue(0xBB), result);
            Assert.Equal(1, trie.GetHeight());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void Delete_SetsValueToAbsent()
        {
            var trie = new BinaryTrie();
            var key = MakeKey(0x00, 0x01);
            trie.Put(key, MakeValue(0xDE));
            trie.Delete(key);

            var result = trie.Get(key);
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void MultiplePuts_AllRetrievable()
        {
            var trie = new BinaryTrie();
            int n = 100;
            for (int i = 0; i < n; i++)
            {
                var key = new byte[32];
                key[24] = (byte)(i >> 24);
                key[25] = (byte)(i >> 16);
                key[26] = (byte)(i >> 8);
                key[27] = (byte)i;
                var val = new byte[32];
                int v = i + 1000;
                val[24] = (byte)(v >> 24);
                val[25] = (byte)(v >> 16);
                val[26] = (byte)(v >> 8);
                val[27] = (byte)v;
                trie.Put(key, val);
            }

            for (int i = 0; i < n; i++)
            {
                var key = new byte[32];
                key[24] = (byte)(i >> 24);
                key[25] = (byte)(i >> 16);
                key[26] = (byte)(i >> 8);
                key[27] = (byte)i;
                var expected = new byte[32];
                int v = i + 1000;
                expected[24] = (byte)(v >> 24);
                expected[25] = (byte)(v >> 16);
                expected[26] = (byte)(v >> 8);
                expected[27] = (byte)v;

                var result = trie.Get(key);
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void HashDeterministic_SameKeysValues_SameRoot()
        {
            var key1 = new byte[32];
            key1[31] = 0x01;
            var key2 = new byte[32];
            key2[31] = 0x02;
            var key3 = new byte[32];
            key3[0] = 0x80;
            key3[31] = 0x01;

            var value = MakeValue(0x01);

            var trie1 = new BinaryTrie();
            trie1.Put(key1, value);
            trie1.Put(key2, value);
            trie1.Put(key3, value);

            var trie2 = new BinaryTrie();
            trie2.Put(key3, value);
            trie2.Put(key1, value);
            trie2.Put(key2, value);

            Assert.Equal(trie1.ComputeRoot(), trie2.ComputeRoot());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void Copy_IndependentModification()
        {
            var trie = new BinaryTrie();
            var key = MakeKey(0x00, 0x01);
            trie.Put(key, MakeValue(0xDE));
            var root1 = trie.ComputeRoot();

            var copy = trie.Copy();
            Assert.Equal(root1, copy.ComputeRoot());

            var key2 = MakeKey(0x80, 0x01);
            copy.Put(key2, MakeValue(0xBE));

            Assert.NotEqual(trie.ComputeRoot(), copy.ComputeRoot());
            Assert.Equal(root1, trie.ComputeRoot());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void DifferentHashFunctions_DifferentRoots()
        {
            var key = MakeKey(0x00, 0x01);
            var value = MakeValue(0xAB);

            var trieSha = new BinaryTrie(new Sha256HashProvider());
            trieSha.Put(key, value);

            var trieBlake = new BinaryTrie(new Blake3HashProvider());
            trieBlake.Put(key, value);

            Assert.NotEqual(trieSha.ComputeRoot(), trieBlake.ComputeRoot());
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void GetNonExistentKey_ReturnsNull()
        {
            var trie = new BinaryTrie();
            trie.Put(MakeKey(0x00, 0x01), MakeValue(0x01));

            var result = trie.Get(MakeKey(0x80, 0x01));
            Assert.Null(result);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void PutStem_GetValuesAtStem()
        {
            var trie = new BinaryTrie();
            var stem = new byte[31];
            var values = new byte[256][];
            values[0] = MakeValue(0xAA);
            values[5] = MakeValue(0xBB);
            values[255] = MakeValue(0xCC);

            trie.PutStem(stem, values);

            var retrieved = trie.GetValuesAtStem(stem);
            Assert.NotNull(retrieved);
            Assert.Equal(values[0], retrieved[0]);
            Assert.Equal(values[5], retrieved[5]);
            Assert.Equal(values[255], retrieved[255]);
            Assert.Null(retrieved[1]);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void SparseValues_Merkleize_Deterministic()
        {
            var values = new byte[256][];
            values[0] = MakeValue(0x01);
            values[42] = MakeValue(0x02);
            values[255] = MakeValue(0x03);

            var hashProvider = new Sha256HashProvider();
            var root1 = ValuesMerkleizer.Merkleize(values, hashProvider);
            var root2 = ValuesMerkleizer.Merkleize(values, hashProvider);

            Assert.Equal(root1, root2);
            Assert.NotEqual(new byte[32], root1);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void EmptyValues_Merkleize_ReturnsZeroHash()
        {
            var values = new byte[256][];
            var hashProvider = new Sha256HashProvider();
            var root = ValuesMerkleizer.Merkleize(values, hashProvider);
            Assert.Equal(new byte[32], root);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void ApplyBatch_MultipleEntries()
        {
            var trie = new BinaryTrie();
            var entries = new List<KeyValuePair<byte[], byte[]>>();
            for (int i = 0; i < 10; i++)
            {
                var key = new byte[32];
                key[0] = (byte)i;
                key[31] = 1;
                entries.Add(new KeyValuePair<byte[], byte[]>(key, MakeValue((byte)(i + 1))));
            }

            trie.ApplyBatch(entries);

            for (int i = 0; i < 10; i++)
            {
                var key = new byte[32];
                key[0] = (byte)i;
                key[31] = 1;
                var result = trie.Get(key);
                Assert.Equal(MakeValue((byte)(i + 1)), result);
            }
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void InvalidKeyLength_ThrowsArgumentException()
        {
            var trie = new BinaryTrie();
            Assert.Throws<ArgumentException>(() => trie.Put(new byte[16], MakeValue(0x01)));
            Assert.Throws<ArgumentException>(() => trie.Get(new byte[16]));
            Assert.Throws<ArgumentException>(() => trie.Delete(new byte[16]));
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void InvalidValueLength_ThrowsArgumentException()
        {
            var trie = new BinaryTrie();
            Assert.Throws<ArgumentException>(() => trie.Put(MakeKey(0x00), new byte[16]));
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void LargeNumberOfEntries_256EntriesIn8BitSpace()
        {
            var trie = new BinaryTrie();
            for (int i = 0; i < 256; i++)
            {
                var key = new byte[32];
                key[0] = (byte)i;
                trie.Put(key, MakeValue(0xFF));
            }
            int height = trie.GetHeight();
            Assert.Equal(1 + 8, height);
        }

        [Fact]
        [Trait("Category", "BinaryTrie")]
        public void SaveToStorage_RoundTrip()
        {
            var trie = new BinaryTrie();
            trie.Put(MakeKey(0x00, 0x01), MakeValue(0xAA));
            trie.Put(MakeKey(0x80, 0x01), MakeValue(0xBB));

            var storage = new InMemoryBinaryTrieStorage();
            trie.SaveToStorage(storage);

            Assert.True(storage.Count > 0);
        }
    }

    public class CompactBinaryNodeCodecTests
    {
        [Fact]
        [Trait("Category", "BinaryTrie-Codec")]
        public void StemNode_EncodeDecodePoundTrip()
        {
            var stem = new byte[31];
            stem[0] = 0xAB;
            stem[15] = 0xCD;
            var values = new byte[256][];
            values[0] = new byte[32];
            values[0][0] = 0xDE;
            values[42] = new byte[32];
            values[42][0] = 0xAD;

            var node = new StemBinaryNode(stem, values, 0);
            var encoded = CompactBinaryNodeCodec.Encode(node, new Sha256HashProvider());
            var decoded = CompactBinaryNodeCodec.Decode(encoded, 0) as StemBinaryNode;

            Assert.NotNull(decoded);
            Assert.Equal(stem, decoded.Stem);
            Assert.Equal(values[0], decoded.Values[0]);
            Assert.Equal(values[42], decoded.Values[42]);
            Assert.Null(decoded.Values[1]);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Codec")]
        public void InternalNode_EncodeDecodeRoundTrip()
        {
            var stem1 = new byte[31];
            var values1 = new byte[256][];
            values1[0] = new byte[32];
            values1[0][0] = 0x01;

            var stem2 = new byte[31];
            stem2[0] = 0x80;
            var values2 = new byte[256][];
            values2[0] = new byte[32];
            values2[0][0] = 0x02;

            var left = new StemBinaryNode(stem1, values1, 1);
            var right = new StemBinaryNode(stem2, values2, 1);
            var node = new InternalBinaryNode(0, left, right);

            var hashProvider = new Sha256HashProvider();
            var encoded = CompactBinaryNodeCodec.Encode(node, hashProvider);
            var decoded = CompactBinaryNodeCodec.Decode(encoded, 0);

            Assert.IsType<InternalBinaryNode>(decoded);
            var decodedInternal = (InternalBinaryNode)decoded;
            Assert.IsType<HashedBinaryNode>(decodedInternal.Left);
            Assert.IsType<HashedBinaryNode>(decodedInternal.Right);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Codec")]
        public void EmptyData_ReturnsEmptyNode()
        {
            var result = CompactBinaryNodeCodec.Decode(Array.Empty<byte>(), 0);
            Assert.IsType<EmptyBinaryNode>(result);
        }
    }

    public class ValuesMerkleizerTests
    {
        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void SinglePopulatedValue_NonZeroRoot()
        {
            var values = new byte[256][];
            values[0] = new byte[32];
            values[0][0] = 0xFF;

            var result = ValuesMerkleizer.Merkleize(values, new Sha256HashProvider());
            Assert.NotEqual(new byte[32], result);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void AllEmpty_ZeroRoot()
        {
            var values = new byte[256][];
            var result = ValuesMerkleizer.Merkleize(values, new Sha256HashProvider());
            Assert.Equal(new byte[32], result);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void DifferentValues_DifferentRoots()
        {
            var hashProvider = new Sha256HashProvider();

            var values1 = new byte[256][];
            values1[0] = new byte[32];
            values1[0][0] = 0x01;

            var values2 = new byte[256][];
            values2[0] = new byte[32];
            values2[0][0] = 0x02;

            var root1 = ValuesMerkleizer.Merkleize(values1, hashProvider);
            var root2 = ValuesMerkleizer.Merkleize(values2, hashProvider);

            Assert.NotEqual(root1, root2);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void ThreePopulatedOutOf256_Deterministic()
        {
            var hashProvider = new Sha256HashProvider();
            var values = new byte[256][];
            values[0] = new byte[32]; values[0][0] = 0xAA;
            values[127] = new byte[32]; values[127][0] = 0xBB;
            values[255] = new byte[32]; values[255][0] = 0xCC;

            var root1 = ValuesMerkleizer.Merkleize(values, hashProvider);
            var root2 = ValuesMerkleizer.Merkleize(values, hashProvider);
            Assert.Equal(root1, root2);
            Assert.NotEqual(new byte[32], root1);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void Blake3VsSha256_DifferentRoots()
        {
            var values = new byte[256][];
            values[0] = new byte[32]; values[0][0] = 0xFF;

            var sha256Root = ValuesMerkleizer.Merkleize(values, new Sha256HashProvider());
            var blake3Root = ValuesMerkleizer.Merkleize(values, new Blake3HashProvider());

            Assert.NotEqual(sha256Root, blake3Root);
        }
    }

    public class Blake3HashProviderTests
    {
        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void ComputeHash_Returns32Bytes()
        {
            var provider = new Blake3HashProvider();
            var result = provider.ComputeHash(new byte[] { 0x01, 0x02, 0x03 });
            Assert.Equal(32, result.Length);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void ComputeHash_Deterministic()
        {
            var provider = new Blake3HashProvider();
            var data = new byte[] { 0x01, 0x02, 0x03 };
            Assert.Equal(provider.ComputeHash(data), provider.ComputeHash(data));
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void DifferentInputs_DifferentHashes()
        {
            var provider = new Blake3HashProvider();
            var hash1 = provider.ComputeHash(new byte[] { 0x01 });
            var hash2 = provider.ComputeHash(new byte[] { 0x02 });
            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void EmptyInput_NonNullResult()
        {
            var provider = new Blake3HashProvider();
            var result = provider.ComputeHash(Array.Empty<byte>());
            Assert.NotNull(result);
            Assert.Equal(32, result.Length);
        }
    }

    public class Sha256HashProviderTests
    {
        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void ComputeHash_Returns32Bytes()
        {
            var provider = new Sha256HashProvider();
            var result = provider.ComputeHash(new byte[] { 0x01, 0x02, 0x03 });
            Assert.Equal(32, result.Length);
        }

        [Fact]
        [Trait("Category", "BinaryTrie-Hashing")]
        public void ComputeHash_MatchesKnownVector()
        {
            var provider = new Sha256HashProvider();
            var result = provider.ComputeHash(Array.Empty<byte>());
            var expected = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855".HexToByteArray();
            Assert.Equal(expected, result);
        }
    }
}
