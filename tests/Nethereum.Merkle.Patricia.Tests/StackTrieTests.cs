using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class StackTrieTests
    {
        private static readonly ByteArrayComparer Bytes = new ByteArrayComparer();

        private static int CompareKeys(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return a[i].CompareTo(b[i]);
            }
            return 0;
        }

        private static List<KeyValuePair<byte[], byte[]>> RandomRows(int count, int seed)
        {
            var random = new Random(seed);
            var rows = new Dictionary<byte[], byte[]>(Bytes);
            while (rows.Count < count)
            {
                var key = new byte[32];
                random.NextBytes(key);
                var value = new byte[1 + random.Next(40)];
                random.NextBytes(value);
                rows[key] = value;
            }
            return Sorted(rows);
        }

        private static List<KeyValuePair<byte[], byte[]>> ClusteredRows(int seed)
        {
            var random = new Random(seed);
            var rows = new Dictionary<byte[], byte[]>(Bytes);
            var penultimates = new byte[] { 0x00, 0x01, 0x10, 0x11, 0xa0 };
            var lasts = new byte[] { 0x00, 0x05, 0x0f, 0x50 };
            for (int cluster = 0; cluster < 3; cluster++)
            {
                var basePrefix = new byte[30];
                random.NextBytes(basePrefix);
                foreach (var penultimate in penultimates)
                {
                    foreach (var last in lasts)
                    {
                        var key = new byte[32];
                        Buffer.BlockCopy(basePrefix, 0, key, 0, 30);
                        key[30] = penultimate;
                        key[31] = last;
                        rows[key] = new[] { (byte)(1 + random.Next(0x7e)) };
                    }
                }
            }
            return Sorted(rows);
        }

        private static List<KeyValuePair<byte[], byte[]>> Sorted(Dictionary<byte[], byte[]> rows)
        {
            var list = rows.ToList();
            list.Sort((x, y) => CompareKeys(x.Key, y.Key));
            return list;
        }

        private static List<KeyValuePair<byte[], byte[]>> Merge(params List<KeyValuePair<byte[], byte[]>>[] sets)
        {
            var rows = new Dictionary<byte[], byte[]>(Bytes);
            foreach (var set in sets)
                foreach (var row in set) rows[row.Key] = row.Value;
            return Sorted(rows);
        }

        private static PatriciaTrie BuildPatricia(IEnumerable<KeyValuePair<byte[], byte[]>> rows, ITrieNodeStore store = null, byte[] owner = null)
        {
            var trie = store == null ? new PatriciaTrie() : new PatriciaTrie(store, owner);
            foreach (var row in rows) trie.Put(row.Key, row.Value);
            return trie;
        }

        private static StackTrie StackHash(IEnumerable<KeyValuePair<byte[], byte[]>> rows, byte[] prefixNibbles = null, NodeEmitted onNode = null)
        {
            var stack = new StackTrie(prefixNibbles, onNode);
            foreach (var row in rows) stack.Update(row.Key, row.Value);
            return stack;
        }

        private static byte[] Nibbles(byte[] key, int count) => key.ConvertToNibbles().Take(count).ToArray();

        private static IEnumerable<KeyValuePair<byte[], byte[]>> UnderPrefix(IEnumerable<KeyValuePair<byte[], byte[]>> rows, byte[] prefix)
            => rows.Where(r => Nibbles(r.Key, prefix.Length).AreTheSame(prefix));

        private static Node NodeStartingAt(Node root, byte[] path)
        {
            var node = root;
            var depth = 0;
            while (true)
            {
                if (node is HashNode hashNode) { node = hashNode.InnerNode; continue; }
                if (depth == path.Length) return node;
                if (node is BranchNode branch)
                {
                    node = branch.Children[path[depth]];
                    depth++;
                    if (node == null || node is EmptyNode) return null;
                    continue;
                }
                if (node is ExtendedNode extension)
                {
                    if (depth + extension.Nibbles.Length > path.Length) return null;
                    for (int i = 0; i < extension.Nibbles.Length; i++)
                    {
                        if (extension.Nibbles[i] != path[depth + i]) return null;
                    }
                    depth += extension.Nibbles.Length;
                    node = extension.InnerNode;
                    continue;
                }
                return null;
            }
        }

        private static byte[] ParentReferenceOf(Node node)
        {
            var encoded = node.GetEncodedData();
            return encoded.Length >= 32 ? node.GetHash() : encoded;
        }

        private static void CountNodes(Node node, ref int total, ref int atLeast32)
        {
            if (node is HashNode hashNode) node = hashNode.InnerNode;
            if (node == null || node is EmptyNode) return;
            total++;
            if (node.GetEncodedData().Length >= 32) atLeast32++;
            if (node is BranchNode branch)
            {
                foreach (var child in branch.Children) CountNodes(child, ref total, ref atLeast32);
            }
            else if (node is ExtendedNode extension)
            {
                CountNodes(extension.InnerNode, ref total, ref atLeast32);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(17)]
        [InlineData(1000)]
        [InlineData(100000)]
        public void Given_SortedKeyValueSetsOfSize0_1_2_17_1000_And100k_When_HashedByStackTrie_Then_TheRootEqualsPatriciaTrie(int size)
        {
            var rows = RandomRows(size, 7000 + size);
            var expected = BuildPatricia(rows).Root.GetHash();

            var stack = StackHash(rows);

            Assert.Equal(expected.ToHex(), stack.Hash().ToHex());
            Assert.Equal(expected.ToHex(), stack.Reference().ToHex());
            if (size == 0) Assert.Equal(DefaultValues.EMPTY_TRIE_HASH.ToHex(), stack.Reference().ToHex());
        }

        [Fact]
        public void Given_ClusteredKeysWhoseLeavesAndBranchesAreEmbedded_When_HashedByStackTrie_Then_TheRootEqualsPatriciaTrieAndTheEmbeddedLeavesAreNotEmitted()
        {
            var rows = ClusteredRows(11);
            var expected = BuildPatricia(rows).Root.GetHash();
            var emitted = 0;

            var stack = StackHash(rows, null, (path, blob) => emitted++);

            Assert.Equal(expected.ToHex(), stack.Hash().ToHex());
            Assert.True(emitted < rows.Count, $"emitted {emitted} nodes for {rows.Count} keys, so no leaf was embedded");
        }

        [Fact]
        public void Given_KeysUnderAnOddAndAnEvenNibblePrefix_When_HashedWithThePrefixStripped_Then_TheReferenceEqualsTheParentsChildReferenceInTheCommittedTrie()
        {
            var rows = Merge(RandomRows(4096, 21), ClusteredRows(22));
            var trie = BuildPatricia(rows, new InMemoryPathNodeStore());
            trie.SaveNodesToStorage();
            var clusteredKey = ClusteredRows(22)[0].Key;

            var candidates = new List<byte[]>();
            foreach (var length in new[] { 1, 2, 3, 4 })
                foreach (var row in rows.Take(64)) candidates.Add(Nibbles(row.Key, length));
            foreach (var length in new[] { 61, 62, 63, 64 }) candidates.Add(Nibbles(clusteredKey, length));

            var checkedOdd = 0;
            var checkedEven = 0;
            var checkedEmbedded = 0;
            var checkedHashed = 0;
            foreach (var prefix in candidates)
            {
                var node = NodeStartingAt(trie.Root, prefix);
                if (node == null) continue;
                var expected = ParentReferenceOf(node);

                var actual = StackHash(UnderPrefix(rows, prefix), prefix).Reference();

                Assert.Equal(expected.ToHex(), actual.ToHex());
                if (prefix.Length % 2 == 1) checkedOdd++; else checkedEven++;
                if (expected.Length < 32) checkedEmbedded++; else checkedHashed++;
            }

            Assert.True(checkedOdd > 0, "no odd-length prefix was checked");
            Assert.True(checkedEven > 0, "no even-length prefix was checked");
            Assert.True(checkedEmbedded > 0, "no embedded (<32 byte) child reference was checked");
            Assert.True(checkedHashed > 0, "no hashed child reference was checked");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Given_ACommittedPathKeyedTrie_When_TheSameKeysAreStackHashed_Then_TheEmittedPathBlobSetEqualsTheNodesThePathStoreHolds(bool storageOwner)
        {
            var owner = storageOwner ? new Sha3Keccack().CalculateHash(new byte[] { 0x42 }) : null;
            var rows = Merge(RandomRows(3000, 31), ClusteredRows(32));
            var store = new InMemoryPathNodeStore();
            var trie = BuildPatricia(rows, store, owner);
            trie.SaveNodesToStorage();
            int total = 0, atLeast32 = 0;
            CountNodes(trie.Root, ref total, ref atLeast32);
            var emitted = new Dictionary<byte[], byte[]>(Bytes);

            var stack = StackHash(rows, null, (path, blob) => emitted.Add(path, blob));

            Assert.Equal(trie.Root.GetHash().ToHex(), stack.Hash().ToHex());
            Assert.True(total > atLeast32, "the trie has no node under 32 bytes, so the skip is not exercised");
            Assert.Equal(atLeast32, store.Count);
            Assert.Equal(store.Count, emitted.Count);
            foreach (var node in emitted)
            {
                Assert.True(node.Value.Length >= 32);
                var reference = new HashNode { Hash = new Sha3Keccack().CalculateHash(node.Value), Owner = owner, Path = node.Key };
                var stored = store.Get(reference);
                Assert.NotNull(stored);
                Assert.Equal(node.Value.ToHex(), stored.ToHex());
            }
        }

        private static Dictionary<byte[], byte[]> EmittedByWholeTrie(IEnumerable<KeyValuePair<byte[], byte[]>> rows)
        {
            var whole = new Dictionary<byte[], byte[]>(Bytes);
            StackHash(rows, null, (path, blob) => whole.Add(path, blob)).Hash();
            return whole;
        }

        private static List<KeyValuePair<byte[], byte[]>> NodesUnder(Dictionary<byte[], byte[]> nodes, byte[] prefix)
            => nodes.Where(n => n.Key.Length >= prefix.Length && n.Key.Take(prefix.Length).ToArray().AreTheSame(prefix)).ToList();

        private static void AssertSameNodes(List<KeyValuePair<byte[], byte[]>> expected, Dictionary<byte[], byte[]> emitted, string context)
        {
            Assert.True(expected.Count == emitted.Count, $"{context}: expected {expected.Count} nodes, emitted {emitted.Count}");
            foreach (var node in expected)
            {
                Assert.True(emitted.TryGetValue(node.Key, out var blob), $"{context}: path {node.Key.ToHex()} was not emitted");
                Assert.Equal(node.Value.ToHex(), blob.ToHex());
            }
        }

        [Fact]
        public void Given_ACommittedTrie_When_SubtreesUnderOddAndEvenPrefixesAreStackHashed_Then_EachEmitsExactlyTheWholeTrieNodesUnderItsPrefixAndNeverAnEmbeddedRoot()
        {
            var clustered = ClusteredRows(42);
            var rows = Merge(RandomRows(3000, 41), clustered);
            var trie = BuildPatricia(rows);
            var whole = EmittedByWholeTrie(rows);
            var candidates = new Dictionary<byte[], bool>(Bytes);
            foreach (var length in new[] { 1, 2, 3, 4, 5 })
                foreach (var row in rows.Take(48)) candidates[Nibbles(row.Key, length)] = true;
            foreach (var length in new[] { 61, 62, 63 }) candidates[Nibbles(clustered[0].Key, length)] = true;

            var checkedOddLong = 0;
            var checkedEvenLong = 0;
            var checkedEmbedded = 0;
            var checkedHashed = 0;
            foreach (var prefix in candidates.Keys)
            {
                if (NodeStartingAt(trie.Root, prefix) == null) continue;
                var emitted = new Dictionary<byte[], byte[]>(Bytes);

                var reference = StackHash(UnderPrefix(rows, prefix), prefix, (path, blob) => emitted.Add(path, blob)).Reference();

                AssertSameNodes(NodesUnder(whole, prefix), emitted, $"prefix {prefix.ToHex()}");
                if (reference.Length < 32)
                {
                    Assert.Empty(emitted);
                    checkedEmbedded++;
                }
                else
                {
                    Assert.True(emitted.ContainsKey(prefix), $"the subtree root at {prefix.ToHex()} was not emitted");
                    checkedHashed++;
                }
                if (prefix.Length > 1 && prefix.Length % 2 == 1) checkedOddLong++;
                if (prefix.Length > 1 && prefix.Length % 2 == 0) checkedEvenLong++;
            }

            Assert.True(checkedOddLong > 0, "no odd prefix longer than one nibble was checked");
            Assert.True(checkedEvenLong > 0, "no even prefix was checked");
            Assert.True(checkedEmbedded > 0, "no subtree with an embedded (<32 byte) root was checked");
            Assert.True(checkedHashed > 0, "no subtree with a hashed root was checked");
        }

        [Fact]
        public void Given_KeysThatMoveToTheNextFirstNibble_When_Updated_Then_TheCompletedSubtreeIsEmittedBeforeHash()
        {
            var rows = RandomRows(20000, 91);
            var whole = EmittedByWholeTrie(rows);
            var firstOfNextNibble = rows.FindIndex(r => r.Key[0] >> 4 == 1);
            var emitted = new Dictionary<byte[], byte[]>(Bytes);
            var stack = new StackTrie(null, (path, blob) => emitted.Add(path, blob));

            for (int i = 0; i <= firstOfNextNibble; i++) stack.Update(rows[i].Key, rows[i].Value);

            AssertSameNodes(NodesUnder(whole, new byte[] { 0 }), emitted, "after the first key under nibble 1");
            for (int i = firstOfNextNibble + 1; i < rows.Count; i++) stack.Update(rows[i].Key, rows[i].Value);
            var emittedBeforeHash = emitted.Count;
            Assert.True(emittedBeforeHash >= whole.Count - 65, $"only {emittedBeforeHash} of {whole.Count} nodes were emitted before Hash, so the trie is not streaming");
            stack.Hash();
            Assert.Equal(whole.Count, emitted.Count);
        }

        [Theory]
        [InlineData("equal key")]
        [InlineData("descending key")]
        [InlineData("empty value")]
        [InlineData("null value")]
        public void Given_ANonAscendingOrEmptyValue_When_Updated_Then_ItThrows(string scenario)
        {
            var rows = RandomRows(3, 51);
            var stack = new StackTrie();
            stack.Update(rows[0].Key, rows[0].Value);
            stack.Update(rows[1].Key, rows[1].Value);
            var valid = new byte[] { 0x01 };
            Action update = scenario switch
            {
                "equal key" => () => stack.Update(rows[1].Key, valid),
                "descending key" => () => stack.Update(rows[0].Key, valid),
                "empty value" => () => stack.Update(rows[2].Key, new byte[0]),
                _ => () => stack.Update(rows[2].Key, null),
            };

            Assert.Throws<ArgumentException>(update);

            Assert.Equal(BuildPatricia(rows.Take(2)).Root.GetHash().ToHex(), stack.Hash().ToHex());
        }

        [Fact]
        public void Given_AKeyOutsideThePrefixOrNot32Bytes_When_Updated_Then_ItThrowsAndTheTrieIsUnchanged()
        {
            var rows = RandomRows(400, 61);
            var prefix = Nibbles(rows[0].Key, 1);
            var inside = UnderPrefix(rows, prefix).ToList();
            var outside = rows.First(r => CompareKeys(r.Key, inside[0].Key) > 0 && !Nibbles(r.Key, 1).AreTheSame(prefix));
            var stack = new StackTrie(prefix);
            stack.Update(inside[0].Key, inside[0].Value);

            Assert.Throws<ArgumentException>(() => stack.Update(outside.Key, outside.Value));
            Assert.Throws<ArgumentException>(() => stack.Update(new byte[31], new byte[] { 0x01 }));
            Assert.Throws<ArgumentException>(() => stack.Update(null, new byte[] { 0x01 }));

            Assert.Equal(StackHash(inside.Take(1), prefix).Reference().ToHex(), stack.Reference().ToHex());
        }

        [Fact]
        public void Given_AnExplicitPrefix_When_TheFirstKeyIsOutsideIt_Then_ItThrowsAndTheTrieStaysEmpty()
        {
            var rows = RandomRows(400, 62);
            var prefix = Nibbles(rows[0].Key, 3);
            var outside = rows.First(r => !Nibbles(r.Key, 3).AreTheSame(prefix));
            var emitted = 0;
            var stack = new StackTrie(prefix, (path, blob) => emitted++);

            Assert.Throws<ArgumentException>(() => stack.Update(outside.Key, outside.Value));

            Assert.Equal(DefaultValues.EMPTY_TRIE_HASH.ToHex(), stack.Reference().ToHex());
            Assert.Equal(0, emitted);
        }

        [Fact]
        public void Given_APrefixOfTheWholeKey_When_ThatKeyIsUpdated_Then_ItIsAcceptedAsAnEmbeddedLeaf()
        {
            var rows = RandomRows(1, 63);
            var stack = new StackTrie(Nibbles(rows[0].Key, 64));

            stack.Update(rows[0].Key, new byte[] { 0x01 });

            Assert.True(stack.Reference().Length < 32);
        }

        [Fact]
        public void Given_AHashedStackTrie_When_UpdatedAgain_Then_ItThrows()
        {
            var rows = RandomRows(2, 71);
            var stack = new StackTrie();
            stack.Update(rows[0].Key, rows[0].Value);
            var root = stack.Hash();

            Assert.Throws<InvalidOperationException>(() => stack.Update(rows[1].Key, rows[1].Value));
            Assert.Equal(root.ToHex(), stack.Hash().ToHex());
        }

        [Fact]
        public void Given_APrefixLongerThanTheKeyOrWithANibbleAbove15_When_Constructed_Then_ItThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StackTrie(new byte[65]));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StackTrie(new byte[] { 0x01, 0x10 }));
        }
    }
}
