using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class PatriciaRangeIteratorTests
    {
        private static (PatriciaTrie trie, InMemoryContentNodeStore storage, List<(byte[] keyHash, byte[] value)> entries) BuildTrie(int count)
        {
            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);

            var entries = new List<(byte[] keyHash, byte[] value)>();
            for (int i = 0; i < count; i++)
            {
                var keyHash = keccak.CalculateHash(new[] { (byte)(i >> 8), (byte)(i & 0xff) });
                var value = new byte[] { (byte)(0x80 | (i & 0x7f)), (byte)(i ^ 0x42) };
                entries.Add((keyHash, value));
                trie.Put(keyHash, value);
            }
            trie.SaveDirtyNodesToStorage();

            entries.Sort((a, b) => ByteArrayComparer.Current.Compare(a.keyHash, b.keyHash));
            return (trie, storage, entries);
        }

        [Fact]
        public void EnumerateRange_FromZero_YieldsAllEntriesInLexOrder()
        {
            var (trie, storage, expected) = BuildTrie(256);

            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, new byte[32])
                .ToList();

            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i].keyHash.ToHex(), actual[i].KeyBytes.ToHex());
                Assert.Equal(expected[i].value.ToHex(), actual[i].Value.ToHex());
            }
        }

        [Fact]
        public void EnumerateRange_FromMiddle_StartsAtFirstKeyAtOrAboveStart()
        {
            var (trie, storage, expected) = BuildTrie(256);

            var middleIdx = expected.Count / 2;
            var startKey = expected[middleIdx].keyHash;

            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, startKey)
                .ToList();

            Assert.Equal(expected.Count - middleIdx, actual.Count);
            Assert.Equal(expected[middleIdx].keyHash.ToHex(), actual.First().KeyBytes.ToHex());
            Assert.Equal(expected.Last().keyHash.ToHex(), actual.Last().KeyBytes.ToHex());
        }

        [Fact]
        public void EnumerateRange_FromAboveMax_YieldsNothing()
        {
            var (trie, storage, _) = BuildTrie(256);

            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, FilledHash(0xff))
                .ToList();

            for (int i = 1; i < actual.Count; i++)
                Assert.True(
                    ByteArrayComparer.Current.Compare(actual[i - 1].KeyBytes, actual[i].KeyBytes) < 0,
                    "iterator must emit strictly increasing keys");
        }

        [Fact]
        public void EnumerateRange_WithMaxCount_StopsAtLimit()
        {
            var (trie, storage, expected) = BuildTrie(256);

            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, new byte[32], maxCount: 10)
                .ToList();

            Assert.Equal(10, actual.Count);
            for (int i = 0; i < 10; i++)
                Assert.Equal(expected[i].keyHash.ToHex(), actual[i].KeyBytes.ToHex());
        }

        [Fact]
        public void EnumerateRange_WithMaxResponseBytes_StopsWhenBudgetExceeded()
        {
            var (trie, storage, _) = BuildTrie(256);

            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, new byte[32], maxResponseBytes: 100)
                .ToList();

            Assert.InRange(actual.Count, 2, 4);
        }

        [Fact]
        public void EnumerateRange_LazyHashNodeResolution_DoesNotRequirePreloadedTree()
        {
            var (trie, storage, expected) = BuildTrie(64);

            var freshTrie = PatriciaTrie.LoadFromStorage(trie.Root.GetHash(), (ITrieNodeStore)storage);

            var actual = PatriciaRangeIterator
                .EnumerateRange(freshTrie.Root, storage, new byte[32])
                .ToList();

            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i].keyHash.ToHex(), actual[i].KeyBytes.ToHex());
        }

        [Fact]
        public void EnumerateRange_SingleEntryTrie_YieldsThatEntry()
        {
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var keyHash = new Sha3Keccack().CalculateHash(new byte[] { 1 });
            var value = new byte[] { 0xab, 0xcd };
            trie.Put(keyHash, value);
            trie.SaveDirtyNodesToStorage();

            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, new byte[32])
                .ToList();

            Assert.Single(actual);
            Assert.Equal(keyHash.ToHex(), actual[0].KeyBytes.ToHex());
            Assert.Equal(value.ToHex(), actual[0].Value.ToHex());
        }

        [Fact]
        public void EnumerateRange_StartExactlyOnEntry_IncludesThatEntry()
        {
            var (trie, storage, expected) = BuildTrie(64);

            var pivot = expected[expected.Count / 3];
            var actual = PatriciaRangeIterator
                .EnumerateRange(trie.Root, storage, pivot.keyHash)
                .ToList();

            Assert.Equal(pivot.keyHash.ToHex(), actual.First().KeyBytes.ToHex());
        }

        [Fact]
        public void EnumerateRange_RejectsNonHashKey()
        {
            var (trie, storage, _) = BuildTrie(8);

            Assert.Throws<ArgumentException>(
                () => PatriciaRangeIterator.EnumerateRange(trie.Root, storage, new byte[31]).ToList());
        }

        [Fact]
        public void EnumerateRange_ReleasesEveryNodeItResolved_AndReEnumerationStillMatches()
        {
            var (built, storage, expected) = BuildTrie(256);
            var rootHash = built.Root.GetHash();

            var loaded = PatriciaTrie.LoadFromStorage(rootHash, storage);
            var first = PatriciaRangeIterator.EnumerateRange(loaded.Root, storage, new byte[32]).ToList();
            Assert.Equal(expected.Count, first.Count);

            Assert.Equal(0, CountResolvedHashNodes(loaded.Root));

            var second = PatriciaRangeIterator.EnumerateRange(loaded.Root, storage, new byte[32]).ToList();
            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].KeyBytes.ToHex(), second[i].KeyBytes.ToHex());
                Assert.Equal(first[i].Value.ToHex(), second[i].Value.ToHex());
            }
        }

        [Fact]
        public void EnumerateRange_AbandonedEarly_StillReleasesResolvedNodes()
        {
            var (built, storage, _) = BuildTrie(256);
            var rootHash = built.Root.GetHash();

            var loaded = PatriciaTrie.LoadFromStorage(rootHash, storage);
            foreach (var entry in PatriciaRangeIterator.EnumerateRange(loaded.Root, storage, new byte[32]))
                break;

            Assert.Equal(0, CountResolvedHashNodes(loaded.Root));
        }

        private static int CountResolvedHashNodes(Nodes.Node node)
        {
            switch (node)
            {
                case Nodes.HashNode h:
                    return h.InnerNode != null ? 1 + CountResolvedHashNodes(h.InnerNode) : 0;
                case Nodes.BranchNode b:
                {
                    int sum = 0;
                    foreach (var child in b.Children)
                        if (child != null) sum += CountResolvedHashNodes(child);
                    return sum;
                }
                case Nodes.ExtendedNode e:
                    return e.InnerNode != null ? CountResolvedHashNodes(e.InnerNode) : 0;
                default:
                    return 0;
            }
        }

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }
    }
}
