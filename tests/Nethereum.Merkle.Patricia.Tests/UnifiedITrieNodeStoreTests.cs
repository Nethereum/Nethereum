using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class UnifiedITrieNodeStoreTests
    {
        public static IEnumerable<object[]> ContentStores() => new[]
        {
            new object[] { (Func<ITrieNodeStore>)(() => new InMemoryContentNodeStore()) },
            new object[] { (Func<ITrieNodeStore>)(() => new InMemoryContentNodeStore()) },
        };

        [Theory]
        [MemberData(nameof(ContentStores))]
        public void Content_Store_Commit_Resolves_Every_Node(Func<ITrieNodeStore> factory)
        {
            var (set, referenceable) = BuildNodeSet(owner: null);
            var store = factory();
            store.Commit(set);

            foreach (var node in set.Nodes)
            {
                var reference = RefOf(node);
                Assert.Equal(node.GetEncodedData(), store.Get(reference));
                Assert.True(store.Contains(reference));
            }
            Assert.NotEmpty(referenceable);
            Assert.False(store.Contains(new HashNode { Hash = new byte[32] }));
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "A path-keyed store verifies the node blob against its reference hash on every read", Order = 2)]
        [Fact]
        public void Path_Store_Commit_Resolves_Referenceable_Nodes_And_Rejects_Tamper()
        {
            var (set, referenceable) = BuildNodeSet(owner: null);
            var pathStore = new InMemoryPathNodeStore();
            ((ITrieNodeStore)pathStore).Commit(set);

            foreach (var node in referenceable)
                Assert.Equal(node.GetEncodedData(), ((ITrieNodeStore)pathStore).Get(RefOf(node)));

            var victim = referenceable[0];
            var wrongRef = new HashNode { Hash = new byte[32], Owner = victim.Owner, Path = victim.Path };
            Assert.Throws<InvalidOperationException>(() => ((ITrieNodeStore)pathStore).Get(wrongRef));
        }

        private static HashNode RefOf(Node node)
            => new HashNode { Hash = node.GetHash(), Owner = node.Owner, Path = node.Path };

        private static (TrieNodeSet set, List<Node> referenceable) BuildNodeSet(byte[] owner)
        {
            var backing = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(backing);
            var keccak = new Sha3Keccack();
            for (int i = 0; i < 60; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            var set = new TrieNodeSet();
            var referenceable = new List<Node>();
            Walk(trie.Root, owner, Array.Empty<byte>(), set, referenceable);
            return (set, referenceable);
        }

        private static void Walk(Node node, byte[] owner, byte[] path, TrieNodeSet set, List<Node> referenceable)
        {
            if (node == null || node is EmptyNode) return;
            if (node is HashNode hn)
            {
                if (hn.InnerNode != null) Walk(hn.InnerNode, owner, path, set, referenceable);
                return;
            }

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
    }
}
