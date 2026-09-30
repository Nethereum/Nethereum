using System;
using System.Collections.Generic;
using System.IO;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Nethereum.Documentation;
using Xunit;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbNodeReverseDiffStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();

        public RocksDbNodeReverseDiffStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-nodediff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        private sealed class JournalingStore : ITrieNodeStore
        {
            private readonly RocksDbPathTrieNodeStore _path;
            private readonly RocksDbNodeReverseDiffStore _journal;
            public ulong Block;
            public JournalingStore(RocksDbPathTrieNodeStore path, RocksDbNodeReverseDiffStore journal)
            {
                _path = path; _journal = journal;
            }
            public void Commit(TrieNodeSet set) => _journal.RecordAndCommit(Block, _path, set);
            public byte[] Get(Node r) => _path.Get(r);
            public bool Contains(Node r) => _path.Contains(r);
            public bool ContainsKey(byte[] r) => _path.ContainsKey(r);
            public void Flush() => _path.Flush();
            public void Clear() => _path.Clear();
        }

        private static byte[] K(int i) => _keccak.CalculateHash(new byte[] { (byte)i, 0x11 });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });

        [Fact]
        [NethereumDocExample(DocSection.ChainInfrastructure, "path-keyed-state", "Node-history journal prunes to the retention window")]
        public void PruneBelow_Leaves_Only_The_Retention_Window()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr);
            var shim = new JournalingStore(pathStore, journal);
            var trie = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };

            for (ulong b = 1; b <= 5; b++)
            {
                shim.Block = b;
                for (int i = 0; i < 5; i++) trie.Put(K((int)b * 10 + i), V((int)b));
                trie.SaveDirtyNodesToStorage();
            }

            for (ulong b = 1; b <= 5; b++)
                Assert.True(CountDiffEntries(b) > 0, $"expected diffs for block {b}");

            journal.PruneBelow(3);

            Assert.Equal(0, CountDiffEntries(1));
            Assert.Equal(0, CountDiffEntries(2));
            Assert.True(CountDiffEntries(3) > 0);
            Assert.True(CountDiffEntries(4) > 0);
            Assert.True(CountDiffEntries(5) > 0);
        }

        private int CountDiffEntries(ulong block)
        {
            var be = RocksDbManager.Write64BE(block);
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                var k = it.Key();
                bool match = k.Length >= 8;
                for (int i = 0; i < 8 && match; i++) if (k[i] != be[i]) match = false;
                if (match) n++;
                it.Next();
            }
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
