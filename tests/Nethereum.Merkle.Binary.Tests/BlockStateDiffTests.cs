using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.StateDiff;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Merkle.Binary.Tests
{
    public class BlockStateDiffTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider = new Blake3HashProvider();

        public BlockStateDiffTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SszRoundTrip_EmptyDiff()
        {
            var diff = new BlockStateDiff
            {
                BlockNumber = 42,
                PreStateRoot = new byte[32],
                PostStateRoot = new byte[32]
            };

            var encoded = BlockStateDiffSszEncoder.Encode(diff);
            var decoded = BlockStateDiffSszEncoder.Decode(encoded);

            Assert.Equal(42, decoded.BlockNumber);
            Assert.Equal(diff.PreStateRoot, decoded.PreStateRoot);
            Assert.Equal(diff.PostStateRoot, decoded.PostStateRoot);
            Assert.Empty(decoded.StemDiffs);
            Assert.Empty(decoded.ProofSiblings);

            _output.WriteLine($"Empty diff: {encoded.Length} bytes");
        }

        [Fact]
        public void SszRoundTrip_WithStemDiffs()
        {
            var diff = new BlockStateDiff
            {
                BlockNumber = 100,
                PreStateRoot = GenerateHash(1),
                PostStateRoot = GenerateHash(2),
                StemDiffs = new List<StemDiff>
                {
                    new StemDiff
                    {
                        Stem = new byte[BinaryTrieConstants.StemSize],
                        SuffixDiffs = new List<SuffixDiff>
                        {
                            new SuffixDiff { SuffixIndex = 0, OldValue = new byte[32], NewValue = GenerateHash(10) },
                            new SuffixDiff { SuffixIndex = 64, OldValue = new byte[32], NewValue = GenerateHash(11) }
                        }
                    }
                },
                ProofSiblings = new List<byte[]> { GenerateHash(20), GenerateHash(21) }
            };

            var encoded = BlockStateDiffSszEncoder.Encode(diff);
            var decoded = BlockStateDiffSszEncoder.Decode(encoded);

            Assert.Equal(100, decoded.BlockNumber);
            Assert.Equal(diff.PreStateRoot, decoded.PreStateRoot);
            Assert.Equal(diff.PostStateRoot, decoded.PostStateRoot);
            Assert.Single(decoded.StemDiffs);
            Assert.Equal(2, decoded.StemDiffs[0].SuffixDiffs.Count);
            Assert.Equal(0, decoded.StemDiffs[0].SuffixDiffs[0].SuffixIndex);
            Assert.Equal(64, decoded.StemDiffs[0].SuffixDiffs[1].SuffixIndex);
            Assert.Equal(diff.StemDiffs[0].SuffixDiffs[1].NewValue, decoded.StemDiffs[0].SuffixDiffs[1].NewValue);
            Assert.Equal(2, decoded.ProofSiblings.Count);

            _output.WriteLine($"Diff with 1 stem, 2 suffixes, 2 siblings: {encoded.Length} bytes");
        }

        [Fact]
        public void SszRoundTrip_MultipleStemsMultipleSuffixes()
        {
            var diff = new BlockStateDiff
            {
                BlockNumber = 1000,
                PreStateRoot = GenerateHash(1),
                PostStateRoot = GenerateHash(2)
            };

            for (int s = 0; s < 5; s++)
            {
                var stemDiff = new StemDiff { Stem = GenerateStem(s) };
                for (int i = 0; i < 10; i++)
                {
                    stemDiff.SuffixDiffs.Add(new SuffixDiff
                    {
                        SuffixIndex = (byte)(i * 25),
                        OldValue = GenerateHash(s * 100 + i),
                        NewValue = GenerateHash(s * 100 + i + 50)
                    });
                }
                diff.StemDiffs.Add(stemDiff);
            }

            for (int i = 0; i < 31; i++)
                diff.ProofSiblings.Add(GenerateHash(200 + i));

            var encoded = BlockStateDiffSszEncoder.Encode(diff);
            var decoded = BlockStateDiffSszEncoder.Decode(encoded);

            Assert.Equal(5, decoded.StemDiffs.Count);
            Assert.Equal(10, decoded.StemDiffs[2].SuffixDiffs.Count);
            Assert.Equal(31, decoded.ProofSiblings.Count);

            _output.WriteLine($"5 stems × 10 suffixes + 31 siblings: {encoded.Length} bytes");
        }

        [Fact]
        public void ProduceFromNodeStore_CapturesDirtyStems()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            var addr = "0x1000000000000000000000000000000000000000".HexToByteArray();
            trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                BasicDataLeaf.Pack(0, 0, 1, new EvmUInt256(1000)));
            var preRoot = trie.ComputeRoot();

            trie.SaveToStorage(store);
            store.ClearDirtyTracking();
            store.MarkBlockCommitted(1);

            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, EvmUInt256.Zero),
                PadTo32(new byte[] { 0x42 }));
            var postRoot = trie.ComputeRoot();
            trie.SaveToStorage(store);

            var diff = BlockStateDiffProducer.Produce(1, preRoot, postRoot, store);

            Assert.Equal(1, diff.BlockNumber);
            Assert.Equal(preRoot, diff.PreStateRoot);
            Assert.Equal(postRoot, diff.PostStateRoot);
            Assert.True(diff.StemDiffs.Count > 0, "Should have at least one dirty stem");

            var encoded = BlockStateDiffSszEncoder.Encode(diff);
            var decoded = BlockStateDiffSszEncoder.Decode(encoded);
            Assert.Equal(diff.StemDiffs.Count, decoded.StemDiffs.Count);

            _output.WriteLine($"Block 1 diff: {diff.StemDiffs.Count} stems, {diff.ProofSiblings.Count} proof nodes, {encoded.Length} bytes");
        }

        [Fact]
        public void DiffSize_ScalesWithChanges()
        {
            var store = new InMemoryBinaryTrieNodeStore();
            var trie = new BinaryTrie(_hashProvider);
            var keyDeriv = new BinaryTreeKeyDerivation(_hashProvider);

            for (int i = 0; i < 20; i++)
            {
                var addr = new byte[20];
                addr[19] = (byte)i;
                trie.Put(keyDeriv.GetTreeKeyForBasicData(addr),
                    BasicDataLeaf.Pack(0, 0, (ulong)i, new EvmUInt256((ulong)(i * 100))));
            }

            var baseRoot = trie.ComputeRoot();
            trie.SaveToStorage(store);
            store.ClearDirtyTracking();
            store.MarkBlockCommitted(1);

            var addr1 = new byte[20]; addr1[19] = 1;
            trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr1, EvmUInt256.Zero),
                PadTo32(new byte[] { 0xFF }));
            trie.SaveToStorage(store);
            var diff1 = BlockStateDiffProducer.Produce(1, baseRoot, trie.ComputeRoot(), store);
            var size1 = BlockStateDiffSszEncoder.Encode(diff1).Length;

            store.ClearDirtyTracking();
            store.MarkBlockCommitted(2);

            for (int i = 0; i < 10; i++)
            {
                var addr = new byte[20]; addr[19] = (byte)i;
                trie.Put(keyDeriv.GetTreeKeyForStorageSlot(addr, (EvmUInt256)1),
                    PadTo32(new byte[] { (byte)(i + 0x10) }));
            }
            trie.SaveToStorage(store);
            var diff2 = BlockStateDiffProducer.Produce(2, diff1.PostStateRoot, trie.ComputeRoot(), store);
            var size2 = BlockStateDiffSszEncoder.Encode(diff2).Length;

            _output.WriteLine($"1 account change: {size1} bytes ({diff1.StemDiffs.Count} stems)");
            _output.WriteLine($"10 account changes: {size2} bytes ({diff2.StemDiffs.Count} stems)");

            Assert.True(size2 > size1, "More changes should produce larger diff");
        }

        private static byte[] GenerateHash(int seed)
        {
            var hash = new byte[32];
            hash[0] = (byte)(seed >> 24);
            hash[1] = (byte)(seed >> 16);
            hash[2] = (byte)(seed >> 8);
            hash[3] = (byte)seed;
            return hash;
        }

        private static byte[] GenerateStem(int seed)
        {
            var stem = new byte[BinaryTrieConstants.StemSize];
            stem[0] = (byte)seed;
            return stem;
        }

        private static byte[] PadTo32(byte[] value)
        {
            if (value.Length >= 32) return value;
            var padded = new byte[32];
            System.Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
