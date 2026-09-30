using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class TrieTombstoneTracerTests
    {
        private sealed class RecordingNodeStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public readonly List<TrieNodeSet> Commits = new();
            public RecordingNodeStore(ITrieNodeStore inner) { _inner = inner; }
            public void Commit(TrieNodeSet nodes) { Commits.Add(nodes); _inner.Commit(nodes); }
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] root) => _inner.ContainsKey(root);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private static string KeyOf(byte[] owner, byte[] path)
            => (owner ?? new byte[0]).ToHex() + ":" + (path ?? new byte[0]).ToHex();

        private static HashSet<string> ReachableKeyedNodes(Node root)
        {
            var fresh = new RecordingNodeStore(new InMemoryContentNodeStore());
            var trie = new PatriciaTrie(root, fresh);
            trie.SaveNodesToStorage();
            var set = new HashSet<string>();
            foreach (var commit in fresh.Commits)
                foreach (var n in commit.Nodes)
                    if ((n.GetEncodedData()?.Length ?? 0) >= 32)
                        set.Add(KeyOf(n.Owner, n.Path));
            return set;
        }

        [Fact]
        public void PureInsert_Emits_No_Deletes()
        {
            var store = new RecordingNodeStore(new InMemoryContentNodeStore());
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie(store) { Tracer = new TrieTracer() };
            for (int i = 0; i < 64; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            trie.SaveDirtyNodesToStorage();

            Assert.Single(store.Commits);
            Assert.Empty(store.Commits[0].Deletes);
        }

        [Fact]
        public void Resave_Without_Mutation_Emits_No_Deletes()
        {
            var store = new RecordingNodeStore(new InMemoryContentNodeStore());
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie(store) { Tracer = new TrieTracer() };
            for (int i = 0; i < 40; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i }), Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            trie.SaveDirtyNodesToStorage();

            trie.SaveDirtyNodesToStorage();

            Assert.Empty(store.Commits[1].Deletes);
        }

        [Fact]
        public void Delete_And_Collapse_Emits_Tombstones_So_Inserted_Minus_Deleted_Equals_Reachable()
        {
            var store = new RecordingNodeStore(new InMemoryContentNodeStore());
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie(store) { Tracer = new TrieTracer() };

            var keys = new List<byte[]>();
            for (int i = 0; i < 128; i++)
            {
                var k = keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
                keys.Add(k);
                trie.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            }
            trie.SaveDirtyNodesToStorage();

            for (int i = 0; i < keys.Count; i += 3)
                trie.Delete(keys[i]);
            trie.SaveDirtyNodesToStorage();

            Assert.True(store.Commits[1].Deletes.Count > 0, "expected tombstones from the delete/collapse pass");

            var inserted = new HashSet<string>();
            foreach (var commit in store.Commits)
                foreach (var n in commit.Nodes)
                    if ((n.GetEncodedData()?.Length ?? 0) >= 32)
                        inserted.Add(KeyOf(n.Owner, n.Path));

            var deleted = new HashSet<string>();
            foreach (var commit in store.Commits)
                foreach (var d in commit.Deletes)
                    if ((d.PrevBlob?.Length ?? 0) >= 32)
                        deleted.Add(KeyOf(d.Owner, d.Path));

            var net = new HashSet<string>(inserted);
            net.ExceptWith(deleted);

            Assert.Equal(ReachableKeyedNodes(trie.Root), net);

            for (int i = 0; i < keys.Count; i++)
            {
                var expected = (i % 3 == 0) ? null : Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
                Assert.Equal(expected, trie.Get(keys[i]));
            }
        }
    }
}
