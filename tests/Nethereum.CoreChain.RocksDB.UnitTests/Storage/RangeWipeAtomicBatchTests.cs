using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Storage
{
    public class RangeWipeAtomicBatchTests : IDisposable
    {
        private readonly string _dir;
        private static readonly Sha3KeccackHashProvider _hp = Sha3KeccackHashProvider.Instance;
        private static readonly Sha3Keccack _keccak = new();

        public RangeWipeAtomicBatchTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-rangewipe-atomic-" + Guid.NewGuid().ToString("N"));
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

        private static RocksDbStorageOptions PathKeyedOptions(string dataDir) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            PathKeyedState = true,
            TrieNodeHistoryBlocks = 100_000,
            TrieNodeHistoryIndex = true,
        };

        private static byte[] K(int i) => _keccak.CalculateHash(new byte[] { (byte)i, 0x9C });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i });
        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }

        private static async Task SeedBlock1Async(RocksDbChainStoreBundle bundle, byte[] owner)
        {
            bundle.NodeCommitBlockSource.Arm(1);
            var storage = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            for (int i = 0; i < 16; i++) storage.Put(K(i), V(i + 1));
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, Hash32(1)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();
        }

        private static System.Collections.Generic.Dictionary<string, byte[]> DumpCf(RocksDbManager mgr, string cf)
        {
            var result = new System.Collections.Generic.Dictionary<string, byte[]>();
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
                $"[{cf}] row count differs: old-immediate={dumpA.Count} new-atomic-batch={dumpB.Count}");
            foreach (var kv in dumpA)
            {
                Assert.True(dumpB.TryGetValue(kv.Key, out var bVal), $"[{cf}] key {kv.Key} present old, missing new");
                Assert.Equal(kv.Value, bVal);
            }
        }

        [Fact]
        public async Task RangeWipe_AtomicBatch_IsByteIdentical_ToOldImmediateRecordAndDeleteRange()
        {
            var dirOld = SubDir("old-immediate");
            var dirNew = SubDir("new-atomic");

            using var mgrOld = new RocksDbManager(PathKeyedOptions(dirOld));
            using var mgrNew = new RocksDbManager(PathKeyedOptions(dirNew));

            var bundleOld = RocksDbChainStoreBundle.FromManager(mgrOld, dirOld, ownsManager: false);
            var bundleNew = RocksDbChainStoreBundle.FromManager(mgrNew, dirNew, ownsManager: false);

            var owner = _keccak.CalculateHash(new byte[] { 0x51 });

            await SeedBlock1Async(bundleOld, owner);
            await SeedBlock1Async(bundleNew, owner);

            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_STATE_TRIE_STORAGE);
            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_NODE_HISTORY);
            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_NODE_HISTORY_INDEX);
            Assert.True(DumpCf(mgrOld, RocksDbManager.CF_STATE_TRIE_STORAGE).Count > 0, "test setup: expected seeded storage rows");

            var pathStoreOld = new RocksDbPathTrieNodeStore(mgrOld);
            var journalOld = new RocksDbNodeReverseDiffStore(mgrOld, buildKeyMajorIndex: true);
            journalOld.RecordAndDeleteRange(2, pathStoreOld, owner);

            bundleNew.NodeCommitBlockSource.Arm(2);
            ((IContractStorageWipeable)bundleNew.StateTrieNodes).DeleteRange(owner);
            bundleNew.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundleNew).FlushBlockAsync(null, 2, Hash32(2)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundleNew).DrainAsync();

            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_STATE_TRIE_STORAGE);
            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_NODE_HISTORY);
            AssertCfIdentical(mgrOld, mgrNew, RocksDbManager.CF_NODE_HISTORY_INDEX);

            Assert.Equal(0, DumpCf(mgrOld, RocksDbManager.CF_STATE_TRIE_STORAGE).Count);
            Assert.Equal(0, DumpCf(mgrNew, RocksDbManager.CF_STATE_TRIE_STORAGE).Count);
        }

        [Fact]
        public async Task RangeWipe_SameBlockAsNodePut_WipeStagesBeforePuts_RepopulationOfSameKeySurvives()
        {
            var dir = SubDir("same-block-wipe-and-put");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0x62 });

            bundle.NodeCommitBlockSource.Arm(1);
            var storage1 = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage1.Put(K(0), V(1));
            storage1.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, Hash32(1)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var keysAfterBlock1 = new System.Collections.Generic.HashSet<string>(DumpCf(mgr, RocksDbManager.CF_STATE_TRIE_STORAGE).Keys);
            Assert.True(keysAfterBlock1.Count > 0, "test setup: expected the seeded single-key subtree to land on disk");

            bundle.NodeCommitBlockSource.Arm(2);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            var storage2 = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage2.Put(K(0), V(99));
            storage2.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 2, Hash32(2)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var afterBlock2 = DumpCf(mgr, RocksDbManager.CF_STATE_TRIE_STORAGE);
            var keysAfterBlock2 = new System.Collections.Generic.HashSet<string>(afterBlock2.Keys);
            Assert.Equal(keysAfterBlock1, keysAfterBlock2);

            var loaded = PatriciaTrie.LoadFromStorage(storage2.Root.GetHash(), bundle.StateTrieNodes, owner);
            Assert.Equal(V(99), loaded.Get(K(0)));
        }
    }
}
