using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Xunit;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.Merkle.Patricia.Tests
{
    public class UnifiedSaveTests
    {
        public static IEnumerable<object[]> Stores() => new[]
        {
            new object[] { (Func<ITrieNodeStore>)(() => new InMemoryContentNodeStore()) },
            new object[] { (Func<ITrieNodeStore>)(() => new InMemoryPathNodeStore()) },
        };

        [Theory]
        [MemberData(nameof(Stores))]
        public void Unified_Save_Then_Reload_RoundTrips(Func<ITrieNodeStore> factory)
        {
            var store = factory();
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie(store);
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

            trie.SaveNodesToStorage();
            var rootHash = trie.Root.GetHash();

            var reloaded = new PatriciaTrie(rootHash, store);
            for (int i = 0; i < keys.Count; i++)
                Assert.Equal(values[i], reloaded.Get(keys[i]));
        }

        [Fact]
        public void Storage_Trie_Owner_Threaded_So_Tries_Coexist_In_One_Path_Store()
        {
            ITrieNodeStore store = new InMemoryPathNodeStore();
            var keccak = new Sha3Keccack();
            var owner = keccak.CalculateHash(new byte[] { 0xC0, 0xDE });

            var keys = new List<byte[]>();
            var storageValues = new List<byte[]>();
            var accountValues = new List<byte[]>();
            for (int i = 0; i < 60; i++)
            {
                keys.Add(keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) }));
                storageValues.Add(Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
                accountValues.Add(Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 100) }));
            }

            var storageTrie = new PatriciaTrie(store, owner);
            for (int i = 0; i < keys.Count; i++) storageTrie.Put(keys[i], storageValues[i]);
            storageTrie.SaveNodesToStorage();
            var storageRoot = storageTrie.Root.GetHash();

            var accountTrie = new PatriciaTrie(store);
            for (int i = 0; i < keys.Count; i++) accountTrie.Put(keys[i], accountValues[i]);
            accountTrie.SaveNodesToStorage();
            var accountRoot = accountTrie.Root.GetHash();

            var reloadedStorage = new PatriciaTrie(storageRoot, store, owner);
            var reloadedAccount = new PatriciaTrie(accountRoot, store);
            for (int i = 0; i < keys.Count; i++)
            {
                Assert.Equal(storageValues[i], reloadedStorage.Get(keys[i]));
                Assert.Equal(accountValues[i], reloadedAccount.Get(keys[i]));
            }
        }

        [Fact]
        public void Unified_Content_Save_Is_Deterministic_Byte_For_Byte()
        {
            var backing = new InMemoryContentNodeStore();
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie(backing);
            for (int i = 0; i < 200; i++)
                trie.Put(keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) }),
                         Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));

            var first = new InMemoryContentNodeStore();
            new PatriciaTrie(trie.Root, first).SaveNodesToStorage();

            var second = new InMemoryContentNodeStore();
            new PatriciaTrie(trie.Root, second).SaveNodesToStorage();

            Assert.Equal(first.Storage.Count, second.Storage.Count);
            foreach (var kv in first.Storage)
            {
                Assert.True(second.Storage.ContainsKey(kv.Key));
                Assert.Equal(kv.Value, second.Storage[kv.Key]);
            }
        }
    }
}
