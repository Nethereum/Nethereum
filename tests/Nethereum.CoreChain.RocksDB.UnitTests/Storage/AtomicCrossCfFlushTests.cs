using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class AtomicCrossCfFlushTests : IDisposable
    {
        private readonly string _dir;
        private static readonly Sha3KeccackHashProvider _hp = Sha3KeccackHashProvider.Instance;
        private static readonly Sha3Keccack _keccak = new();

        public AtomicCrossCfFlushTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-atomiccrosscf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private string SubDir(string name)
        {
            var path = Path.Combine(_dir, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private const string AddrX = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrY = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string AddrZ = "0xcccccccccccccccccccccccccccccccccccccc";
        private const string AddrW = "0xdddddddddddddddddddddddddddddddddddddd";

        private static byte[] CodeHash(byte b) { var h = new byte[32]; h[0] = b; return h; }
        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }

        private static RocksDbStorageOptions PathKeyedOptions(string dataDir) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            PathKeyedState = true,
            TrieNodeHistoryBlocks = 100_000,
            TrieNodeHistoryIndex = true,
        };

        private static async Task SeedAsync(RocksDbStateStore raw)
        {
            await raw.SaveAccountAsync(AddrX, new Account { Balance = 100, Nonce = 1 });
            await raw.SaveStorageAsync(AddrX, 1, new byte[] { 0x01 });

            await raw.SaveAccountAsync(AddrY, new Account { Balance = 200, Nonce = 1 });
            await raw.SaveStorageAsync(AddrY, 1, new byte[] { 0x02 });
            await raw.SaveStorageAsync(AddrY, 2, new byte[] { 0x03 });

            await raw.SaveAccountAsync(AddrZ, new Account { Balance = 300, Nonce = 1 });
            await raw.SaveStorageAsync(AddrZ, 1, new byte[] { 0x04 });
            await raw.SaveStorageAsync(AddrZ, 2, new byte[] { 0x05 });
        }

        private static FlatStateBatch BuildFlushBatch()
        {
            var newCodeHash = CodeHash(0x99);
            return new FlatStateBatch(
                deletedAccountAddresses: new[] { AddrX },
                clearedStorageAddresses: new[] { AddrY },
                nonZeroStorage: new[] { (AddrZ, (BigInteger)3, new byte[] { 0x06 }) },
                deletedSlots: new[] { (AddrZ, (BigInteger)1) },
                accounts: new[]
                {
                    (AddrW, new Account { Balance = 400, Nonce = 1, CodeHash = newCodeHash }),
                    (AddrZ, new Account { Balance = 350, Nonce = 2 }),
                },
                code: new[] { (newCodeHash, new byte[] { 0x60, 0x60 }) });
        }

        private sealed class CapturingStore : ITrieNodeStore
        {
            private readonly RocksDbPathTrieNodeStore _inner;
            public TrieNodeSet LastCommitted;
            public CapturingStore(RocksDbPathTrieNodeStore inner) => _inner = inner;
            public void Commit(TrieNodeSet nodes) { LastCommitted = nodes; _inner.Commit(nodes); }
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Flush() => _inner.Flush();
            public void Clear() => _inner.Clear();
        }

        private static byte[] K(int i) => _keccak.CalculateHash(new byte[] { (byte)i, 0x77 });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i });

        private TrieNodeSet BuildRealisticNodeSet()
        {
            var scratchDir = SubDir("nodeset-scratch-" + Guid.NewGuid().ToString("N"));
            using var scratchMgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = scratchDir, PathKeyedState = true });
            var scratchPathStore = new RocksDbPathTrieNodeStore(scratchMgr);
            var capture = new CapturingStore(scratchPathStore);
            var trie = new PatriciaTrie(capture, _hp) { Tracer = new TrieTracer() };

            for (int i = 0; i < 12; i++) trie.Put(K(i), V(i + 1));
            trie.SaveDirtyNodesToStorage();

            trie.Delete(K(2));
            trie.Delete(K(5));
            trie.Put(K(20), V(99));
            trie.SaveDirtyNodesToStorage();

            Assert.True(capture.LastCommitted.Count > 0, "test setup: expected node inserts in the captured set");
            Assert.True(capture.LastCommitted.Deletes.Count > 0, "test setup: expected at least one node delete to exercise the delete branch");
            return capture.LastCommitted;
        }

        private static Dictionary<string, byte[]> DumpCf(RocksDbManager mgr, string cf)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = mgr.CreateIterator(cf);
            it.SeekToFirst();
            while (it.Valid())
            {
                result[Convert.ToHexString(it.Key())] = it.Value();
                it.Next();
            }
            return result;
        }

        private static void AssertCfIdentical(RocksDbManager a, RocksDbManager b, string cf)
        {
            var dumpA = DumpCf(a, cf);
            var dumpB = DumpCf(b, cf);
            Assert.True(dumpA.Count == dumpB.Count,
                $"[{cf}] row count differs: legacy-path={dumpA.Count} atomic-path={dumpB.Count}");
            foreach (var kv in dumpA)
            {
                Assert.True(dumpB.TryGetValue(kv.Key, out var bVal), $"[{cf}] key {kv.Key} present in legacy path, missing in atomic path");
                Assert.Equal(kv.Value, bVal);
            }
        }

        private static void AssertDumpEqual(string cf, Dictionary<string, byte[]> before, Dictionary<string, byte[]> after)
        {
            Assert.True(before.Count == after.Count, $"[{cf}] row count changed after the faulted call: before={before.Count} after={after.Count}");
            foreach (var kv in before)
            {
                Assert.True(after.TryGetValue(kv.Key, out var aVal), $"[{cf}] key {kv.Key} present before the faulted call, missing after");
                Assert.Equal(kv.Value, aVal);
            }
        }

        [Fact]
        public async Task FlushStateAtomically_ProducesByteIdenticalCfContents_VsLegacyThreeCommitPath()
        {
            var dirA = SubDir("legacy");
            var dirB = SubDir("atomic");

            using var mgrA = new RocksDbManager(PathKeyedOptions(dirA));
            using var mgrB = new RocksDbManager(PathKeyedOptions(dirB));

            var rawA = new RocksDbStateStore(mgrA);
            var pathStoreA = new RocksDbPathTrieNodeStore(mgrA);
            var journalA = new RocksDbNodeReverseDiffStore(mgrA, buildKeyMajorIndex: true);
            var metadataA = new RocksDbChainMetadataStore(mgrA);

            var bundleB = RocksDbChainStoreBundle.FromManager(mgrB, dirB, ownsManager: false);
            var rawB = new RocksDbStateStore(mgrB);
            var pathStoreB = new RocksDbPathTrieNodeStore(mgrB);

            await SeedAsync(rawA);
            await SeedAsync(rawB);

            var flat = BuildFlushBatch();
            var nodeSet = BuildRealisticNodeSet();
            const ulong block = 777;
            var hash = Hash32(1);

            await rawA.ApplyBatchAsync(flat);
            journalA.RecordAndCommit(block, pathStoreA, nodeSet);
            metadataA.CommitDurableState(block, hash);
            metadataA.Commit(block, hash);

            bundleB.FlushStateAtomically(flat, nodeSet, block, hash, pathStoreB);

            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_ACCOUNTS);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_STORAGE);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_CODE);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_TRIE_ACCOUNT);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_STATE_TRIE_STORAGE);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_NODE_HISTORY);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_NODE_HISTORY_INDEX);
            AssertCfIdentical(mgrA, mgrB, RocksDbManager.CF_METADATA);

            Assert.True(DumpCf(mgrA, RocksDbManager.CF_STATE_ACCOUNTS).Count > 0);
            Assert.True(DumpCf(mgrA, RocksDbManager.CF_NODE_HISTORY).Count > 0);
            Assert.True(DumpCf(mgrA, RocksDbManager.CF_METADATA).Count > 0);

            Assert.Equal(block, metadataA.GetDurableStateBlock());
            Assert.Equal(block, ((RocksDbChainMetadataStore)bundleB.Metadata).GetDurableStateBlock());

            Assert.Equal(block, metadataA.GetLastBlock());
            Assert.Equal(block, ((RocksDbChainMetadataStore)bundleB.Metadata).GetLastBlock());

            Assert.Null(await rawB.GetAccountAsync(AddrX));
            Assert.Null(await rawB.GetStorageAsync(AddrY, 1));
            Assert.Null(await rawB.GetStorageAsync(AddrY, 2));
            Assert.Null(await rawB.GetStorageAsync(AddrZ, 1));
            Assert.NotNull(await rawB.GetStorageAsync(AddrZ, 2));
            Assert.NotNull(await rawB.GetStorageAsync(AddrZ, 3));
            Assert.NotNull(await rawB.GetAccountAsync(AddrW));

        }

        [Fact]
        public void FlushStateAtomically_SingleWriteCall_ComposesAllCategories()
        {
            var dir = SubDir("atomicity-writecount");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var pathStore = new RocksDbPathTrieNodeStore(mgr);

            var flat = BuildFlushBatch();
            var nodeSet = BuildRealisticNodeSet();

            var writesBefore = mgr.WriteCallCount;
            var commitsBefore = mgr.NativeCommitCount;

            bundle.FlushStateAtomically(flat, nodeSet, block: 5, hash: Hash32(3), pathStore);

            Assert.Equal(writesBefore + 1, mgr.WriteCallCount);
            Assert.Equal(commitsBefore + 1, mgr.NativeCommitCount);
        }

        [Fact]
        public async Task FlushStateAtomically_IsAllOrNothing_OnStagingFault()
        {
            var dir = SubDir("atomicity-fault");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var raw = new RocksDbStateStore(mgr);

            await SeedAsync(raw);

            var flat = BuildFlushBatch();
            var nodeSet = BuildRealisticNodeSet();

            var cfs = new[]
            {
                RocksDbManager.CF_STATE_ACCOUNTS, RocksDbManager.CF_STATE_STORAGE, RocksDbManager.CF_STATE_CODE,
                RocksDbManager.CF_STATE_TRIE_ACCOUNT, RocksDbManager.CF_STATE_TRIE_STORAGE,
                RocksDbManager.CF_NODE_HISTORY, RocksDbManager.CF_NODE_HISTORY_INDEX, RocksDbManager.CF_METADATA,
            };
            var before = new Dictionary<string, Dictionary<string, byte[]>>();
            foreach (var cf in cfs) before[cf] = DumpCf(mgr, cf);
            var writesBefore = mgr.WriteCallCount;

            Assert.Throws<ArgumentNullException>(() =>
                bundle.FlushStateAtomically(flat, nodeSet, block: 5, hash: Hash32(4), pathStore: null));

            Assert.Equal(writesBefore, mgr.WriteCallCount);
            foreach (var cf in cfs) AssertDumpEqual(cf, before[cf], DumpCf(mgr, cf));

            Assert.True(before[RocksDbManager.CF_STATE_ACCOUNTS].Count > 0);
        }
    }
}
