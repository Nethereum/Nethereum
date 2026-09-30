using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class HeldStoreTrieTests
    {
        public static IEnumerable<object[]> Stores() => new[]
        {
            new object[] { (Func<ITrieNodeStore>)(() => new InMemoryContentNodeStore()) },
            new object[] { (Func<ITrieNodeStore>)(() => new InMemoryPathNodeStore()) },
        };

        [NethereumDocExample(DocSection.ChainInfrastructure, "patricia-trie", "Build a trie, commit it, and reload it by root through the held node store", Order = 1)]
        [Theory]
        [MemberData(nameof(Stores))]
        public void Reads_Through_Held_Store_Without_Threading(Func<ITrieNodeStore> factory)
        {
            var (store, rootHash, keys, values) = BuildAndCommit(factory);

            var reloaded = new PatriciaTrie(rootHash, store);
            for (int i = 0; i < keys.Count; i++)
                Assert.Equal(values[i], reloaded.Get(keys[i]));
        }

        [Theory]
        [MemberData(nameof(Stores))]
        public void Put_And_Delete_Reload_Through_Held_Store(Func<ITrieNodeStore> factory)
        {
            var (store, rootHash, keys, values) = BuildAndCommit(factory);
            var keccak = new Sha3Keccack();

            var reloaded = new PatriciaTrie(rootHash, store);

            reloaded.Delete(keys[0]);
            Assert.Null(reloaded.Get(keys[0]));
            Assert.Equal(values[1], reloaded.Get(keys[1]));

            var newKey = keccak.CalculateHash(new byte[] { 0xEE, 0xFF });
            var newValue = Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x7A });
            reloaded.Put(newKey, newValue);
            Assert.Equal(newValue, reloaded.Get(newKey));
        }

        [Theory]
        [MemberData(nameof(Stores))]
        public void GenerateProof_Through_Held_Store_Is_Not_Empty(Func<ITrieNodeStore> factory)
        {
            var (store, rootHash, keys, values) = BuildAndCommit(factory);

            var reloaded = PatriciaTrie.LoadFromStorage(rootHash, store);
            var proof = ProofGenerator.GenerateProof(reloaded, keys[0]);

            Assert.NotNull(proof);
            Assert.NotEmpty(proof);
            Assert.Equal(rootHash, new Sha3Keccack().CalculateHash(proof[0]));
        }

        private static (ITrieNodeStore store, byte[] rootHash, List<byte[]> keys, List<byte[]> values) BuildAndCommit(Func<ITrieNodeStore> factory)
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

            var store = factory();
            var set = new TrieNodeSet();
            Walk(trie.Root, owner: null, path: Array.Empty<byte>(), set);
            store.Commit(set);
            return (store, rootHash, keys, values);
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
