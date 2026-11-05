using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class StackTriePathStoreParityTests : IDisposable
    {
        private static readonly ByteArrayComparer Bytes = new ByteArrayComparer();
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"stacktrie_{Guid.NewGuid():N}");
        private readonly RocksDbManager _mgr;

        public StackTriePathStoreParityTests()
        {
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        public void Dispose()
        {
            _mgr.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private static int CompareKeys(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return a[i].CompareTo(b[i]);
            }
            return 0;
        }

        private static List<KeyValuePair<byte[], byte[]>> Rows(int seed)
        {
            var random = new Random(seed);
            var rows = new Dictionary<byte[], byte[]>(Bytes);
            while (rows.Count < 3000)
            {
                var key = new byte[32];
                random.NextBytes(key);
                var value = new byte[1 + random.Next(40)];
                random.NextBytes(value);
                rows[key] = value;
            }
            for (int cluster = 0; cluster < 3; cluster++)
            {
                var basePrefix = new byte[30];
                random.NextBytes(basePrefix);
                foreach (var penultimate in new byte[] { 0x00, 0x01, 0x10, 0x11, 0xa0 })
                {
                    foreach (var last in new byte[] { 0x00, 0x05, 0x0f, 0x50 })
                    {
                        var key = new byte[32];
                        Buffer.BlockCopy(basePrefix, 0, key, 0, 30);
                        key[30] = penultimate;
                        key[31] = last;
                        rows[key] = new[] { (byte)(1 + random.Next(0x7e)) };
                    }
                }
            }
            var list = rows.ToList();
            list.Sort((x, y) => CompareKeys(x.Key, y.Key));
            return list;
        }

        private Dictionary<byte[], byte[]> StoredRows(byte[] owner)
        {
            var cf = owner == null ? RocksDbManager.CF_STATE_TRIE_ACCOUNT : RocksDbManager.CF_STATE_TRIE_STORAGE;
            var rows = new Dictionary<byte[], byte[]>(Bytes);
            using var iterator = _mgr.CreateIterator(cf);
            for (iterator.SeekToFirst(); iterator.Valid(); iterator.Next())
            {
                var key = iterator.Key();
                var path = owner == null ? key : key.Skip(owner.Length).ToArray();
                rows.Add(path, iterator.Value());
            }
            return rows;
        }

        private static int CountEmbeddedNodes(Node node)
        {
            if (node is HashNode hashNode) node = hashNode.InnerNode;
            if (node == null || node is EmptyNode) return 0;
            var count = node.GetEncodedData().Length < 32 ? 1 : 0;
            if (node is BranchNode branch)
            {
                foreach (var child in branch.Children) count += CountEmbeddedNodes(child);
            }
            else if (node is ExtendedNode extension)
            {
                count += CountEmbeddedNodes(extension.InnerNode);
            }
            return count;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Given_ACommittedPathKeyedTrie_When_TheSameKeysAreStackHashed_Then_TheEmittedPathBlobSetEqualsTheRowsAppendCommitWrote(bool storageOwner)
        {
            var owner = storageOwner ? new Sha3Keccack().CalculateHash(new byte[] { 0x42 }) : null;
            var rows = Rows(storageOwner ? 81 : 82);
            var trie = new PatriciaTrie(new RocksDbPathTrieNodeStore(_mgr), owner);
            foreach (var row in rows) trie.Put(row.Key, row.Value);
            trie.SaveNodesToStorage();
            var stored = StoredRows(owner);
            var emitted = new Dictionary<byte[], byte[]>(Bytes);

            var stack = new StackTrie(null, (path, blob) => emitted.Add(path, blob));
            foreach (var row in rows) stack.Update(row.Key, row.Value);

            Assert.Equal(trie.Root.GetHash().ToHex(), stack.Hash().ToHex());
            Assert.True(CountEmbeddedNodes(trie.Root) > 0, "no node is under 32 bytes, so the skip is not exercised");
            AssertEmittedEqualsStored(stored.ToList(), emitted, "whole trie");
        }

        private static void AssertEmittedEqualsStored(List<KeyValuePair<byte[], byte[]>> stored, Dictionary<byte[], byte[]> emitted, string context)
        {
            Assert.True(stored.Count == emitted.Count, $"{context}: {stored.Count} stored rows, {emitted.Count} emitted nodes");
            foreach (var row in stored)
            {
                Assert.True(emitted.TryGetValue(row.Key, out var blob), $"{context}: stored path {row.Key.ToHex()} was not emitted");
                Assert.Equal(row.Value.ToHex(), blob.ToHex());
            }
        }

        private static bool StartsWith(byte[] path, byte[] prefix)
            => path.Length >= prefix.Length && path.Take(prefix.Length).ToArray().AreTheSame(prefix);

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

        [Fact]
        public void Given_ACommittedPathKeyedTrie_When_SubtreesUnderOddAndEvenPrefixesAreStackHashed_Then_EachEmittedSetEqualsTheRowsStoredUnderItsPrefix()
        {
            var rows = Rows(83);
            var trie = new PatriciaTrie(new RocksDbPathTrieNodeStore(_mgr));
            foreach (var row in rows) trie.Put(row.Key, row.Value);
            trie.SaveNodesToStorage();
            var stored = StoredRows(null);
            var candidates = new Dictionary<byte[], bool>(Bytes);
            var keyNibbles = rows.Select(r => r.Key.ConvertToNibbles()).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                foreach (var length in i < 64 ? new[] { 2, 3, 4, 5, 61, 62, 63 } : new[] { 61, 62, 63 })
                {
                    var prefix = keyNibbles[i].Take(length).ToArray();
                    if (NodeStartingAt(trie.Root, prefix) != null) candidates[prefix] = true;
                }
            }

            var checkedOdd = 0;
            var checkedEven = 0;
            var checkedEmbedded = 0;
            var checkedHashed = 0;
            foreach (var prefix in candidates.Keys)
            {
                var emitted = new Dictionary<byte[], byte[]>(Bytes);
                var stack = new StackTrie(prefix, (path, blob) => emitted.Add(path, blob));
                for (int i = 0; i < rows.Count; i++)
                {
                    if (StartsWith(keyNibbles[i], prefix)) stack.Update(rows[i].Key, rows[i].Value);
                }

                var reference = stack.Reference();

                AssertEmittedEqualsStored(stored.Where(r => StartsWith(r.Key, prefix)).ToList(), emitted, $"prefix {prefix.ToHex()}");
                if (reference.Length < 32) checkedEmbedded++; else checkedHashed++;
                if (prefix.Length % 2 == 1) checkedOdd++; else checkedEven++;
            }

            Assert.True(checkedOdd > 0, "no odd prefix was checked");
            Assert.True(checkedEven > 0, "no even prefix was checked");
            Assert.True(checkedEmbedded > 0, "no subtree with an embedded (<32 byte) root was checked");
            Assert.True(checkedHashed > 0, "no subtree with a hashed root was checked");
        }
    }
}
