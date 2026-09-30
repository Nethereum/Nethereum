using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HashNodeStoreAndBufferTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;

        public HashNodeStoreAndBufferTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-hashnode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
        }

        [Fact]
        public void RocksDbTrieNodeStore_Content_Addressed_Commit_Resolves_All()
        {
            ITrieNodeStore store = new RocksDbTrieNodeStore(_mgr);
            var set = BuildNodeSet();

            store.Commit(set);

            foreach (var node in set.Nodes)
            {
                var reference = RefOf(node);
                Assert.Equal(node.GetEncodedData(), store.Get(reference));
                Assert.True(store.Contains(reference));
            }
        }

        [Fact]
        public void Buffer_Node_Commit_Read_Your_Writes_Then_Drains_To_Inner()
        {
            var inner = new RocksDbTrieNodeStore(_mgr);
            var buffer = new BufferedTrieStorage(inner, _mgr);
            ITrieNodeStore ns = buffer;

            var set = BuildNodeSet();
            ns.Commit(set);
            Assert.True(buffer.PendingCount > 0);

            foreach (var node in set.Nodes)
                Assert.Equal(node.GetEncodedData(), ns.Get(RefOf(node)));
            Assert.Null(((ITrieNodeStore)inner).Get(RefOf(set.Nodes[0])));

            buffer.FlushBuffer();
            Assert.Equal(0, buffer.PendingCount);

            foreach (var node in set.Nodes)
                Assert.Equal(node.GetEncodedData(), ((ITrieNodeStore)inner).Get(RefOf(node)));
        }

        private static HashNode RefOf(Node node)
            => new HashNode { Hash = node.GetHash(), Owner = node.Owner, Path = node.Path };

        private static TrieNodeSet BuildNodeSet()
        {
            var backing = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(backing);
            var keccak = new Sha3Keccack();
            for (int i = 0; i < 60; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            var set = new TrieNodeSet();
            Walk(trie.Root, set);
            return set;
        }

        private static void Walk(Node node, TrieNodeSet set)
        {
            if (node == null || node is EmptyNode) return;
            if (node is HashNode hn) { if (hn.InnerNode != null) Walk(hn.InnerNode, set); return; }
            set.Add(node);
            if (node is BranchNode b) foreach (var c in b.Children) Walk(c, set);
            else if (node is ExtendedNode e) Walk(e.InnerNode, set);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
