using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class PathStoreAppliesTombstonesTests
    {
        private static int ReachableKeyedCount(Node root)
        {
            var fresh = new InMemoryPathNodeStore();
            new PatriciaTrie(root, fresh).SaveNodesToStorage();
            return fresh.Count;
        }

        [Fact]
        public void Delete_Collapse_Leaves_Store_With_Exactly_The_Reachable_Nodes()
        {
            var store = new InMemoryPathNodeStore();
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie(store) { Tracer = new TrieTracer() };

            var keys = new List<byte[]>();
            for (int i = 0; i < 160; i++)
            {
                var k = keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
                keys.Add(k);
                trie.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            }
            trie.SaveDirtyNodesToStorage();

            for (int i = 0; i < keys.Count; i += 3)
                trie.Delete(keys[i]);
            for (int i = 1; i < keys.Count; i += 7)
                trie.Put(keys[i], Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 200), 0x01 }));
            trie.SaveDirtyNodesToStorage();

            Assert.Equal(ReachableKeyedCount(trie.Root), store.Count);

            var reloaded = new PatriciaTrie(trie.Root, store);
            for (int i = 0; i < keys.Count; i++)
            {
                byte[] expected =
                    (i % 7 == 1) ? Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 200), 0x01 })
                    : (i % 3 == 0) ? null
                    : Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
                Assert.Equal(expected, reloaded.Get(keys[i]));
            }
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "key-path-storage", "Account and contract-storage tries share one path-keyed store, separated by owner", Order = 1)]
        [Fact]
        public void Storage_Trie_Owner_Keyed_Deletes_Do_Not_Touch_Account_Trie()
        {
            var store = new InMemoryPathNodeStore();
            var keccak = new Sha3Keccack();
            var owner = keccak.CalculateHash(new byte[] { 0xC0, 0xDE });

            var keys = new List<byte[]>();
            for (int i = 0; i < 96; i++)
                keys.Add(keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) }));

            var account = new PatriciaTrie(store) { Tracer = new TrieTracer() };
            var storage = new PatriciaTrie(store, owner) { Tracer = new TrieTracer() };
            foreach (var k in keys)
            {
                account.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xAC }));
                storage.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x57 }));
            }
            account.SaveDirtyNodesToStorage();
            storage.SaveDirtyNodesToStorage();

            foreach (var k in keys) storage.Delete(k);
            storage.SaveDirtyNodesToStorage();

            var reloadedAccount = new PatriciaTrie(account.Root, store);
            foreach (var k in keys)
                Assert.Equal(Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xAC }), reloadedAccount.Get(k));
        }
    }
}
