using System;
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
    public class RocksDbNodeHistoryIndexTests : IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbManager _mgr;
        private static readonly IHashProvider _hp = new Sha3KeccackHashProvider();
        private static readonly Sha3Keccack _keccak = new();
        private static readonly byte[] StoragePath = new byte[0];

        public RocksDbNodeHistoryIndexTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-nodeidx-" + Guid.NewGuid().ToString("N"));
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

        private (byte[] owner, RocksDbNodeReverseDiffStore journal) BuildScenario(bool buildIndex)
        {
            var pathStore = new RocksDbPathTrieNodeStore(_mgr);
            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: buildIndex);
            var shim = new JournalingStore(pathStore, journal);

            var owner = _keccak.CalculateHash(new byte[] { 0xAB, 0xCD });
            var slot = _keccak.CalculateHash(new byte[] { 0x77 });

            var account = new PatriciaTrie(shim, _hp) { Tracer = new TrieTracer() };
            var storage = new PatriciaTrie(shim, owner, _hp) { Tracer = new TrieTracer() };

            shim.Block = 1;
            for (int i = 0; i < 10; i++) account.Put(K(i), V(i + 1));
            storage.Put(slot, V(50));
            account.SaveDirtyNodesToStorage();
            storage.SaveDirtyNodesToStorage();

            shim.Block = 2;
            for (int i = 10; i < 13; i++) account.Put(K(i), V(i + 1));
            account.SaveDirtyNodesToStorage();

            shim.Block = 3;
            account.Put(K(13), V(14));
            storage.Put(slot, V(60));
            account.SaveDirtyNodesToStorage();
            storage.SaveDirtyNodesToStorage();

            shim.Block = 4;
            account.Put(K(14), V(15));
            account.SaveDirtyNodesToStorage();

            shim.Block = 5;
            storage.Put(slot, V(70));
            storage.SaveDirtyNodesToStorage();

            pathStore.Flush();
            return (owner, journal);
        }

        [Fact]
        public void FindBlobAsOf_Matches_NodeHistory_Ground_Truth_Across_Overwrite_Chain_And_Absent()
        {
            var (owner, journal) = BuildScenario(buildIndex: true);

            byte[] Expected(ulong n)
            {
                for (ulong b = 1; b <= 6; b++)
                {
                    if (b <= n) continue;
                    var blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(b, owner, StoragePath));
                    if (blob != null) return blob;
                }
                return null;
            }

            for (ulong n = 0; n <= 6; n++)
                AssertBytesEqual(Expected(n), journal.FindBlobAsOf(owner, StoragePath, n), $"N={n}");

            var b1Blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(3, owner, StoragePath));
            var b3Blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(5, owner, StoragePath));

            Assert.Equal(0, journal.FindBlobAsOf(owner, StoragePath, 0).Length);
            Assert.Equal(b1Blob, journal.FindBlobAsOf(owner, StoragePath, 1));
            Assert.Equal(b1Blob, journal.FindBlobAsOf(owner, StoragePath, 2));
            Assert.Equal(b3Blob, journal.FindBlobAsOf(owner, StoragePath, 3));
            Assert.Equal(b3Blob, journal.FindBlobAsOf(owner, StoragePath, 4));
            Assert.Null(journal.FindBlobAsOf(owner, StoragePath, 5));
            Assert.Null(journal.FindBlobAsOf(owner, StoragePath, 6));

            Assert.True(b1Blob.Length >= 32 && b3Blob.Length >= 32);
            Assert.NotEqual(b1Blob, b3Blob);
        }

        [Fact]
        public void PruneBelow_Removes_Index_Rows_Below_Floor_And_FindBlobAsOf_Returns_Retained()
        {
            var (owner, journal) = BuildScenario(buildIndex: true);

            Assert.True(CountIndexRowsBelow(3) > 0, "expected index rows below floor before prune");

            journal.PruneBelow(3);

            Assert.Equal(0, CountIndexRowsBelow(3));

            var retained = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(3, owner, StoragePath));
            Assert.Equal(retained, journal.FindBlobAsOf(owner, StoragePath, 0));

            Assert.Equal(_mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(5, owner, StoragePath)),
                         journal.FindBlobAsOf(owner, StoragePath, 3));
            Assert.Null(journal.FindBlobAsOf(owner, StoragePath, 5));
        }

        [Fact]
        public void Flag_Off_Writes_No_Index_Rows()
        {
            BuildScenario(buildIndex: false);
            Assert.Equal(0, CountIndexRows());
        }

        [Fact]
        public void FindBlobAsOf_Resolves_Shallow_AccountRoot_With_Descendant_Interleaving()
        {
            BuildScenario(buildIndex: true);
            var (rootOwner, rootPath) = (new byte[0], new byte[0]);

            byte[] Expected(ulong n)
            {
                for (ulong b = 1; b <= 6; b++)
                {
                    if (b <= n) continue;
                    var blob = _mgr.Get(RocksDbManager.CF_NODE_HISTORY, HistoryKey(b, rootOwner, rootPath));
                    if (blob != null) return blob;
                }
                return null;
            }

            var journal = new RocksDbNodeReverseDiffStore(_mgr, buildKeyMajorIndex: true);
            for (ulong n = 0; n <= 6; n++)
                AssertBytesEqual(Expected(n), journal.FindBlobAsOf(rootOwner, rootPath, n), $"acctRoot N={n}");

            Assert.Equal(0, journal.FindBlobAsOf(rootOwner, rootPath, 0).Length);
            Assert.Null(journal.FindBlobAsOf(rootOwner, rootPath, 4));
        }

        [Fact]
        public void Index_Prefix_For_Shallow_AccountRoot_Bounds_Only_Its_Own_Versions()
        {
            BuildScenario(buildIndex: true);
            var (rootOwner, rootPath) = (new byte[0], new byte[0]);

            int rootVersions = CountAccountRootNodeHistoryEntries();
            Assert.Equal(4, rootVersions);

            Assert.True(CountRowsWithTag((byte)'A') > rootVersions, "expected descendant account-trie index rows");

            var prefix = NodeIndexPrefix(rootOwner, rootPath);
            int matched = 0;
            using (var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX))
            {
                it.SeekToFirst();
                while (it.Valid())
                {
                    var k = it.Key();
                    if (ByteUtil.StartsWith(k, prefix))
                    {
                        Assert.Equal(prefix.Length + 8, k.Length);
                        matched++;
                    }
                    it.Next();
                }
            }
            Assert.Equal(rootVersions, matched);
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

        private static byte[] NodeIndexPrefix(byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            int ownerLen = acct ? 0 : owner.Length;
            int pathLen = path?.Length ?? 0;
            var prefix = new byte[1 + ownerLen + 1 + pathLen];
            int o = 0;
            prefix[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, prefix, o, ownerLen); o += ownerLen; }
            prefix[o++] = (byte)pathLen;
            if (pathLen > 0) Buffer.BlockCopy(path, 0, prefix, o, pathLen);
            return prefix;
        }

        private int CountAccountRootNodeHistoryEntries()
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                var k = it.Key();
                if (k.Length == 9 && k[8] == (byte)'A') n++;
                it.Next();
            }
            return n;
        }

        private int CountRowsWithTag(byte tag)
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                var k = it.Key();
                if (k.Length >= 1 && k[0] == tag) n++;
                it.Next();
            }
            return n;
        }

        private int CountIndexRows()
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid()) { n++; it.Next(); }
            return n;
        }

        private int CountIndexRowsBelow(ulong floor)
        {
            using var it = _mgr.CreateIterator(RocksDbManager.CF_NODE_HISTORY_INDEX);
            it.SeekToFirst();
            int n = 0;
            while (it.Valid())
            {
                var k = it.Key();
                if (k.Length >= 8)
                {
                    ulong b = 0;
                    for (int i = k.Length - 8; i < k.Length; i++) b = (b << 8) | k[i];
                    if (b < floor) n++;
                }
                it.Next();
            }
            return n;
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual, string ctx)
        {
            if (expected == null) { Assert.True(actual == null, $"{ctx}: expected null, got {actual?.Length} bytes"); return; }
            Assert.True(actual != null, $"{ctx}: expected {expected.Length} bytes, got null");
            Assert.True(ByteUtil.AreEqual(expected, actual), $"{ctx}: byte mismatch");
        }

        public void Dispose()
        {
            _mgr.Dispose();
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
