using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class ReloadInliningInvariantTests
    {
        private sealed class SpyNodeStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            public readonly List<byte[]> Gets = new List<byte[]>();
            public SpyNodeStore(ITrieNodeStore inner) { _inner = inner; }
            public byte[] Get(Node reference) { Gets.Add(reference.GetHash()); return _inner.Get(reference); }
            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] root) => _inner.ContainsKey(root);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        [Fact]
        public void Reload_Inlines_Short_Children_Never_Fetched_By_Hash()
        {
            var comparer = new ByteArrayComparer();
            var backing = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(backing);

            var keys = new List<byte[]>();
            for (byte i = 1; i <= 8; i++)
            {
                var k = new byte[] { (byte)(i << 4) };
                keys.Add(k);
                trie.Put(k, new byte[] { i });
            }

            trie.SaveNodesToStorage();
            var root = trie.Root.GetHash();

            var shortHashes = new HashSet<byte[]>(comparer);
            CollectShortNodeHashes(trie.Root, isRoot: true, shortHashes);
            Assert.NotEmpty(shortHashes);

            var spy = new SpyNodeStore(backing);
            var reloaded = PatriciaTrie.LoadFromStorage(root, spy);
            for (int idx = 0; idx < keys.Count; idx++)
                Assert.Equal(new byte[] { (byte)(idx + 1) }, reloaded.Get(keys[idx]));

            foreach (var g in spy.Gets)
                Assert.False(shortHashes.Contains(g), "an embedded (<32B) node was fetched standalone by hash");

            Assert.Contains(spy.Gets, g => comparer.Equals(g, root));
        }

        private static void CollectShortNodeHashes(Node node, bool isRoot, HashSet<byte[]> acc)
        {
            if (node == null || node is EmptyNode) return;
            if (node is HashNode hn)
            {
                if (hn.InnerNode != null) CollectShortNodeHashes(hn.InnerNode, isRoot, acc);
                return;
            }

            var rlp = node.GetEncodedData();
            if (!isRoot && rlp != null && rlp.Length < 32) acc.Add(node.GetHash());

            if (node is BranchNode b)
                foreach (var child in b.Children) CollectShortNodeHashes(child, false, acc);
            else if (node is ExtendedNode e)
                CollectShortNodeHashes(e.InnerNode, false, acc);
        }
    }
}
