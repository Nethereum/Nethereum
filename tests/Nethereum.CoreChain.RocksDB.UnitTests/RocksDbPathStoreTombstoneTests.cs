using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbPathStoreTombstoneTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public RocksDbPathStoreTombstoneTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-pathtomb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        [Fact]
        public void Delete_Collapse_Leaves_Account_Cf_With_Exactly_The_Reachable_Nodes()
        {
            var store = new RocksDbPathTrieNodeStore(_mgr);
            var trie = new PatriciaTrie(store, _hp) { Tracer = new TrieTracer() };

            var keys = new List<byte[]>();
            for (int i = 0; i < 160; i++)
            {
                var k = _keccak.CalculateHash(new byte[] { (byte)i, (byte)(i >> 8) });
                keys.Add(k);
                trie.Put(k, Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i }));
            }
            trie.SaveDirtyNodesToStorage();
            store.Flush();

            for (int i = 0; i < keys.Count; i += 3)
                trie.Delete(keys[i]);
            for (int i = 1; i < keys.Count; i += 3)
                trie.Put(keys[i], Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 200), 0x01 }));
            trie.SaveDirtyNodesToStorage();
            store.Flush();

            Assert.Equal(ReachableKeyedCount(trie.Root), CountKeys(RocksDbManager.CF_STATE_TRIE_ACCOUNT));

            var reopened = new RocksDbPathTrieNodeStore(_mgr);
            var reloaded = new PatriciaTrie(trie.Root.GetHash(), reopened, _hp);
            for (int i = 0; i < keys.Count; i++)
            {
                byte[] expected =
                    (i % 3 == 0) ? null
                    : (i % 3 == 1) ? Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)(i + 200), 0x01 })
                    : Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });
                Assert.Equal(expected, reloaded.Get(keys[i]));
            }
        }

        private static int ReachableKeyedCount(Node root)
        {
            var fresh = new InMemoryPathNodeStore();
            new PatriciaTrie(root, fresh).SaveNodesToStorage();
            return fresh.Count;
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
