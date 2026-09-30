using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbPathKeyedStateRoundTripTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public RocksDbPathKeyedStateRoundTripTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-pathstate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Path-keyed state round-trips after reopen")]
        public void PathState_Account_And_Storage_RoundTrip_After_Reopen()
        {
            var owner = _keccak.CalculateHash(new byte[] { 0xAB, 0xCD });

            byte[] accountRoot;
            byte[] storageRoot;
            var accountEntries = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);
            var storageEntries = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);

            {
                var store = new RocksDbPathTrieNodeStore(_mgr);

                var accountTrie = new PatriciaTrie(store, _hp);
                var storageTrie = new PatriciaTrie(store, owner, _hp);

                for (int i = 0; i < 128; i++)
                {
                    var k = _keccak.CalculateHash(new byte[] { (byte)i, 0x01 });
                    var v = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 1) });
                    accountTrie.Put(k, v);
                    accountEntries[k] = v;

                    var sk = _keccak.CalculateHash(new byte[] { (byte)i, 0x02 });
                    var sv = Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 7) });
                    storageTrie.Put(sk, sv);
                    storageEntries[sk] = sv;
                }

                storageTrie.SaveNodesToStorage();
                accountTrie.SaveNodesToStorage();
                store.Flush();

                accountRoot = accountTrie.Root.GetHash();
                storageRoot = storageTrie.Root.GetHash();
            }

            var reopened = new RocksDbPathTrieNodeStore(_mgr);

            var loadedAccount = PatriciaTrie.LoadFromStorage(accountRoot, reopened);
            var loadedStorage = PatriciaTrie.LoadFromStorage(storageRoot, reopened, owner);

            foreach (var kvp in accountEntries)
                Assert.Equal(kvp.Value, loadedAccount.Get(kvp.Key));
            foreach (var kvp in storageEntries)
                Assert.Equal(kvp.Value, loadedStorage.Get(kvp.Key));

            Assert.Equal(accountRoot, loadedAccount.Root.GetHash());
            Assert.Equal(storageRoot, loadedStorage.Root.GetHash());
        }

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Two contracts share slot keys without collision")]
        public void PathState_Two_Contracts_Same_Slots_Coexist_Without_Collision()
        {
            var ownerA = _keccak.CalculateHash(new byte[] { 0x0A });
            var ownerB = _keccak.CalculateHash(new byte[] { 0x0B });

            var slotKeys = new List<byte[]>();
            for (int i = 0; i < 64; i++)
                slotKeys.Add(_keccak.CalculateHash(new byte[] { (byte)i }));

            byte[] rootA, rootB;
            {
                var store = new RocksDbPathTrieNodeStore(_mgr);
                var trieA = new PatriciaTrie(store, ownerA, _hp);
                var trieB = new PatriciaTrie(store, ownerB, _hp);
                foreach (var k in slotKeys)
                {
                    trieA.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xAA }));
                    trieB.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xBB }));
                }
                trieA.SaveNodesToStorage();
                trieB.SaveNodesToStorage();
                store.Flush();
                rootA = trieA.Root.GetHash();
                rootB = trieB.Root.GetHash();
            }

            Assert.NotEqual(rootA, rootB);

            var reopened = new RocksDbPathTrieNodeStore(_mgr);
            var loadedA = PatriciaTrie.LoadFromStorage(rootA, reopened, ownerA);
            var loadedB = PatriciaTrie.LoadFromStorage(rootB, reopened, ownerB);

            foreach (var k in slotKeys)
            {
                Assert.Equal(Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xAA }), loadedA.Get(k));
                Assert.Equal(Nethereum.RLP.RLP.EncodeElement(new byte[] { 0xBB }), loadedB.Get(k));
            }
            Assert.Equal(rootA, loadedA.Root.GetHash());
            Assert.Equal(rootB, loadedB.Root.GetHash());
        }

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public static readonly ByteArrayComparer Instance = new();
            public bool Equals(byte[] x, byte[] y) => ByteUtil.AreEqual(x, y);
            public int GetHashCode(byte[] obj)
            {
                unchecked
                {
                    int h = 17;
                    foreach (var b in obj) h = h * 31 + b;
                    return h;
                }
            }
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
