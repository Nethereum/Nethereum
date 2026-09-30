using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Nodes.Rlp;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class UnifiedDecodeTests
    {
        [Fact]
        public void Unified_Decode_Reconstructs_Over_Content_Store()
            => RoundTripThroughUnifiedDecode(new InMemoryContentNodeStore());

        [Fact]
        public void Unified_Decode_Reconstructs_Over_Path_Store()
            => RoundTripThroughUnifiedDecode(new InMemoryPathNodeStore());

        private static void RoundTripThroughUnifiedDecode(ITrieNodeStore store)
        {
            var keccak = new Sha3Keccack();
            var backing = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(backing);
            var keys = new List<byte[]>();
            var values = new List<byte[]>();
            for (int i = 0; i < 200; i++)
            {
                var k = keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
                var v = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
                keys.Add(k);
                values.Add(v);
                trie.Put(k, v);
            }
            var rootHash = trie.Root.GetHash();

            var set = new TrieNodeSet();
            Walk(trie.Root, owner: null, path: Array.Empty<byte>(), set);
            store.Commit(set);

            var rootReference = new HashNode { Hash = rootHash, Owner = null, Path = Array.Empty<byte>() };
            var decodedRoot = new NodeDecoder().Decode(rootReference, store, decodeHashNodes: true);
            var reloaded = new PatriciaTrie(decodedRoot);
            for (int i = 0; i < keys.Count; i++)
                Assert.Equal(values[i], reloaded.Get(keys[i]));
        }

        [Fact]
        public void DecodeInnerNode_Over_ITrieNodeStore_Has_No_Sniff()
        {
            var keccak = new Sha3Keccack();
            var backing = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(backing);
            for (int i = 0; i < 40; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            var rootHash = trie.Root.GetHash();

            ITrieNodeStore store = new InMemoryPathNodeStore();
            var set = new TrieNodeSet();
            Walk(trie.Root, owner: null, path: Array.Empty<byte>(), set);
            store.Commit(set);

            var reference = new HashNode { Hash = rootHash, Owner = null, Path = Array.Empty<byte>() };
            reference.DecodeInnerNode(store, decodeInnerHashNodes: false);
            Assert.NotNull(reference.InnerNode);
            Assert.False(reference.InnerNode is EmptyNode);
        }

        private static void Walk(Node node, byte[] owner, byte[] path, TrieNodeSet set)
        {
            if (node == null || node is EmptyNode) return;
            if (node is HashNode hn) { if (hn.InnerNode != null) Walk(hn.InnerNode, owner, path, set); return; }
            node.Owner = owner;
            node.Path = path;
            set.Add(node);
            if (node is BranchNode b)
            {
                for (int i = 0; i < 16; i++)
                    if (b.Children[i] != null)
                        Walk(b.Children[i], owner, path.ConcatArrays(new byte[] { (byte)i }), set);
            }
            else if (node is ExtendedNode e)
            {
                Walk(e.InnerNode, owner, path.ConcatArrays(e.Nibbles), set);
            }
        }
    }
}
