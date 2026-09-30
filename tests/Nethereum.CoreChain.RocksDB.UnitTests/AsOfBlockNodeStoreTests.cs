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
    public class AsOfBlockNodeStoreTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();
        private static readonly byte[] RootPath = new byte[0];

        public AsOfBlockNodeStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-asof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir, PathKeyedState = true });
        }

        private sealed class JournalingStore : ITrieNodeStore
        {
            private readonly RocksDbPathTrieNodeStore _path;
            private readonly RocksDbNodeReverseDiffStore _journal;
            public ulong Block;
            public JournalingStore(RocksDbPathTrieNodeStore path, RocksDbNodeReverseDiffStore journal)
            { _path = path; _journal = journal; }
            public void Commit(TrieNodeSet set) => _journal.RecordAndCommit(Block, _path, set);
            public byte[] Get(Node r) => _path.Get(r);
            public bool Contains(Node r) => _path.Contains(r);
            public bool ContainsKey(byte[] r) => _path.ContainsKey(r);
            public void Flush() => _path.Flush();
            public void Clear() => _path.Clear();
        }

        private static byte[] K(int i) => _keccak.CalculateHash(new byte[] { (byte)i, 0x11 });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i });

        private sealed class Scenario
        {
            public byte[] Owner;
            public RocksDbNodeReverseDiffStore Journal;
            public RocksDbPathTrieNodeStore PathStore;
            public Dictionary<ulong, byte[]> AccountRootHashByBlock = new();
        }

        private Scenario BuildScenario()
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);
            var shim = new JournalingStore(pathStore, journal);

            var owner = _keccak.CalculateHash(new byte[] { 0xAB, 0xCD });
            var slot = _keccak.CalculateHash(new byte[] { 0x77 });

            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };
            var storage = new PatriciaTrie(shim, owner, _hp) { Tracer = new TrieTracer() };

            var s = new Scenario { Owner = owner, Journal = journal, PathStore = pathStore };

            shim.Block = 1;
            for (int i = 0; i < 10; i++) account.Put(K(i), V(i + 1));
            storage.Put(slot, V(50));
            account.SaveDirtyNodesToStorage();
            storage.SaveDirtyNodesToStorage();
            s.AccountRootHashByBlock[1] = account.Root.GetHash();

            shim.Block = 2;
            for (int i = 10; i < 13; i++) account.Put(K(i), V(i + 1));
            account.SaveDirtyNodesToStorage();
            s.AccountRootHashByBlock[2] = account.Root.GetHash();

            shim.Block = 3;
            account.Put(K(13), V(14));
            storage.Put(slot, V(60));
            account.SaveDirtyNodesToStorage();
            storage.SaveDirtyNodesToStorage();
            s.AccountRootHashByBlock[3] = account.Root.GetHash();

            shim.Block = 4;
            account.Put(K(14), V(15));
            account.SaveDirtyNodesToStorage();
            s.AccountRootHashByBlock[4] = account.Root.GetHash();

            shim.Block = 5;
            storage.Put(slot, V(70));
            storage.SaveDirtyNodesToStorage();
            s.AccountRootHashByBlock[5] = account.Root.GetHash();

            pathStore.Flush();
            return s;
        }

        [Fact]
        public void Get_Node_Returns_AsOfN_Blob_For_Changed_Key()
        {
            var s = BuildScenario();

            var b1Blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(3, s.Owner, RootPath));
            Assert.True(b1Blob != null && b1Blob.Length >= 32);

            var reference = new HashNode(_hp) { Owner = s.Owner, Path = RootPath, Hash = _hp.ComputeHash(b1Blob) };

            var asOf2 = new AsOfBlockNodeStore(s.Journal, s.PathStore, 2);
            Assert.Equal(b1Blob, asOf2.Get(reference));
        }

        [Fact]
        public void Get_Node_Falls_Back_To_Latest_For_Unchanged_Key()
        {
            var s = BuildScenario();

            var headBlob = _mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, s.Owner);
            Assert.NotNull(headBlob);

            var reference = new HashNode(_hp) { Owner = s.Owner, Path = RootPath, Hash = _hp.ComputeHash(headBlob) };

            var asOfHead = new AsOfBlockNodeStore(s.Journal, s.PathStore, 5);
            Assert.Equal(headBlob, asOfHead.Get(reference));
        }

        [Fact]
        public void Get_Node_Returns_Null_For_Absent_At_N()
        {
            var s = BuildScenario();

            var b1Blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(3, s.Owner, RootPath));
            var reference = new HashNode(_hp) { Owner = s.Owner, Path = RootPath, Hash = _hp.ComputeHash(b1Blob) };

            var asOf0 = new AsOfBlockNodeStore(s.Journal, s.PathStore, 0);
            Assert.Null(asOf0.Get(reference));
        }

        [Fact]
        public void ContainsKey_True_For_AccountRoot_AsOfN_False_Otherwise()
        {
            var s = BuildScenario();

            var rootAsOf2 = s.AccountRootHashByBlock[2];

            var asOf2 = new AsOfBlockNodeStore(s.Journal, s.PathStore, 2);
            Assert.True(asOf2.ContainsKey(rootAsOf2));

            var bogus = new byte[32];
            bogus[0] = 0xEE;
            Assert.False(asOf2.ContainsKey(bogus));

            Assert.False(asOf2.ContainsKey(s.AccountRootHashByBlock[4]));
        }

        private static byte[] HistoryKey(ulong block, byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            int ownerLen = acct ? 0 : owner.Length;
            int pathLen = path?.Length ?? 0;
            var key = new byte[8 + 1 + ownerLen + pathLen];
            Buffer.BlockCopy(RocksDbManager.Write64BE(block), 0, key, 0, 8);
            int o = 8;
            key[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, key, o, ownerLen); o += ownerLen; }
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, o, pathLen);
            return key;
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
