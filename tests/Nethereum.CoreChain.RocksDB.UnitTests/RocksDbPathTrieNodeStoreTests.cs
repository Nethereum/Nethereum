using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbPathTrieNodeStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public RocksDbPathTrieNodeStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-path-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "Path-keyed store: commit nodes, resolve by reference, reject a tampered blob")]
        public void PathStore_Commit_Resolves_Referenceable_And_Rejects_Tamper()
        {
            var store = new RocksDbPathTrieNodeStore(_mgr);
            var (set, referenceable) = BuildNodeSet();
            ((ITrieNodeStore)store).Commit(set);

            foreach (var node in referenceable)
                Assert.Equal(node.GetEncodedData(), ((ITrieNodeStore)store).Get(RefOf(node)));

            var victim = referenceable[0];
            var wrongRef = new HashNode { Hash = new byte[32], Owner = victim.Owner, Path = victim.Path };
            Assert.Throws<InvalidOperationException>(() => ((ITrieNodeStore)store).Get(wrongRef));
        }

        [Fact]
        public void PathStore_No_Orphans_On_Clean_Store()
        {
            var store = new RocksDbPathTrieNodeStore(_mgr);
            var (set, referenceable) = BuildNodeSet();
            ((ITrieNodeStore)store).Commit(set);

            Assert.Equal(referenceable.Count, CountKeys(RocksDbManager.CF_STATE_TRIE_ACCOUNT));
        }

        private static HashNode RefOf(Node node)
            => new HashNode { Hash = node.GetHash(), Owner = node.Owner, Path = node.Path };

        private static (TrieNodeSet set, List<Node> referenceable) BuildNodeSet()
        {
            var backing = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(backing);
            var keccak = new Sha3Keccack();
            for (int i = 0; i < 200; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            var set = new TrieNodeSet();
            var referenceable = new List<Node>();
            Walk(trie.Root, owner: null, path: Array.Empty<byte>(), set, referenceable);
            return (set, referenceable);
        }

        private static void Walk(Node node, byte[] owner, byte[] path, TrieNodeSet set, List<Node> referenceable)
        {
            if (node == null || node is EmptyNode) return;
            if (node is HashNode hn) { if (hn.InnerNode != null) Walk(hn.InnerNode, owner, path, set, referenceable); return; }
            node.Owner = owner;
            node.Path = path;
            var rlp = node.GetEncodedData();
            set.Add(node);
            if (rlp != null && rlp.Length >= 32) referenceable.Add(node);
            if (node is BranchNode b)
            {
                for (int i = 0; i < 16; i++)
                    if (b.Children[i] != null)
                        Walk(b.Children[i], owner, path.ConcatArrays(new byte[] { (byte)i }), set, referenceable);
            }
            else if (node is ExtendedNode e)
            {
                Walk(e.InnerNode, owner, path.ConcatArrays(e.Nibbles), set, referenceable);
            }
        }

        private int CountKeys(string cf)
        {
            using var it = _mgr.CreateIterator(cf);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
