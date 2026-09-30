using System;
using System.Collections.Generic;
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
    public class DeferredWindowCorrectnessMatrixTests : IDisposable
    {
        private readonly string _dir;
        private static readonly Sha3KeccackHashProvider _hp = Sha3KeccackHashProvider.Instance;
        private static readonly Sha3Keccack _keccak = new();
        private static readonly byte[] K0 = _keccak.CalculateHash(new byte[] { 0x9C });
        private static readonly byte[] K1 = _keccak.CalculateHash(new byte[] { 0x9D });

        public DeferredWindowCorrectnessMatrixTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-deferred-matrix-" + Guid.NewGuid().ToString("N"));
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

        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i });
        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }

        private static byte[] StorageKey(byte[] owner, byte[] path)
        {
            var key = new byte[owner.Length + path.Length];
            Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
            Buffer.BlockCopy(path, 0, key, owner.Length, path.Length);
            return key;
        }

        private static byte[] ExtractPath(byte[] fullKey, byte[] owner)
        {
            var path = new byte[fullKey.Length - owner.Length];
            Buffer.BlockCopy(fullKey, owner.Length, path, 0, path.Length);
            return path;
        }

        private static byte[] BlockKey(ulong block, byte[] owner, byte[] path)
        {
            var key = new byte[8 + 1 + owner.Length + path.Length];
            Buffer.BlockCopy(RocksDbManager.Write64BE(block), 0, key, 0, 8);
            int o = 8;
            key[o++] = (byte)'O';
            Buffer.BlockCopy(owner, 0, key, o, owner.Length); o += owner.Length;
            Buffer.BlockCopy(path, 0, key, o, path.Length);
            return key;
        }

        private static Dictionary<string, byte[]> DumpOwnerRows(RocksDbManager mgr, byte[] owner)
        {
            var result = new Dictionary<string, byte[]>();
            using var it = mgr.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            it.Seek(owner);
            while (it.Valid())
            {
                var key = it.Key();
                if (!ByteUtil.StartsWith(key, owner)) break;
                result[Convert.ToHexString(key)] = it.Value();
                it.Next();
            }
            return result;
        }

        private static byte[] SoleOwnerPath(RocksDbManager mgr, byte[] owner)
        {
            using var it = mgr.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            it.Seek(owner);
            Assert.True(it.Valid() && ByteUtil.StartsWith(it.Key(), owner),
                "test setup: expected the seeded single-key subtree to land on disk");
            return ExtractPath(it.Key(), owner);
        }

        private static async Task<(byte[] Path, byte[] Blob)> SeedSingleSlotAsync(
            RocksDbManager mgr, RocksDbChainStoreBundle bundle, byte[] owner, byte[] key, byte[] value, ulong block)
        {
            bundle.NodeCommitBlockSource.Arm(block);
            var storage = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage.Put(key, value);
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, block, Hash32((byte)block)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var path = SoleOwnerPath(mgr, owner);
            return (path, mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path)));
        }

        [Fact]
        public async Task SelfDestructInWindowOwner_JournalDrivenRewindToMidWindowTarget_RestoresPreDestructCfBytes()
        {
            var dir = SubDir("selfdestruct-reorg");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0x71 });

            bundle.NodeCommitBlockSource.Arm(1);
            var storage = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage.Put(K0, V(1));
            storage.Put(K1, V(2));
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, Hash32(1)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var preDestructRows = DumpOwnerRows(mgr, owner);
            Assert.True(preDestructRows.Count > 0, "test setup: expected seeded storage rows for owner X");

            bundle.NodeCommitBlockSource.Arm(2);
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            bundle.NodeCommitBlockSource.Clear();

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 4, Hash32(4)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            Assert.Empty(DumpOwnerRows(mgr, owner));

            var journal = new RocksDbNodeReverseDiffStore(mgr, buildKeyMajorIndex: true);
            var stats = journal.MaterializingRewindTo(2);
            Assert.True(stats.EntriesApplied > 0, "test setup: expected the rewind to actually replay entries");

            var restoredRows = DumpOwnerRows(mgr, owner);
            Assert.Equal(preDestructRows.Count, restoredRows.Count);
            foreach (var kv in preDestructRows)
            {
                Assert.True(restoredRows.TryGetValue(kv.Key, out var restored),
                    $"row {kv.Key} missing after rewind to the pre-destruct target");
                Assert.Equal(kv.Value, restored);
            }
        }

        [Fact]
        public async Task NoReorgDestroyedSlot_WindowFlushed_NoResurrection_NoStorageRow()
        {
            var dir = SubDir("no-reorg-resurrection");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0x82 });

            var (path, v0Blob) = await SeedSingleSlotAsync(mgr, bundle, owner, K0, V(1), block: 1);

            bundle.NodeCommitBlockSource.Arm(2);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            bundle.NodeCommitBlockSource.Clear();

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 3, Hash32(3)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            Assert.Null(mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path)));
            Assert.Empty(DumpOwnerRows(mgr, owner));

            var journalRow = mgr.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(2, owner, path));
            Assert.NotNull(journalRow);
            Assert.Equal(v0Blob, journalRow);
        }

        [Fact]
        public async Task DeleteThenRecreateInWindow_RewindToIntermediateBlock_RestoresRecreatedAbsentState_NotStaleDiskBlob()
        {
            var dir = SubDir("delete-then-recreate");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0x93 });
            var controlOwner = _keccak.CalculateHash(new byte[] { 0x94 });

            bundle.NodeCommitBlockSource.Arm(1);
            var storage = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage.Put(K0, V(1));
            storage.SaveDirtyNodesToStorage();
            var controlStorage = new PatriciaTrie(bundle.StateTrieNodes, controlOwner, _hp) { Tracer = new TrieTracer() };
            controlStorage.Put(K0, V(7));
            controlStorage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, Hash32(1)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var path = SoleOwnerPath(mgr, owner);
            var controlPath = SoleOwnerPath(mgr, controlOwner);
            var controlBlob = mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(controlOwner, controlPath));

            bundle.NodeCommitBlockSource.Arm(2);
            storage.Delete(K0);
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            storage.Put(K0, V(99));
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 3, Hash32(3)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var loaded = PatriciaTrie.LoadFromStorage(storage.Root.GetHash(), bundle.StateTrieNodes, owner);
            Assert.Equal(V(99), loaded.Get(K0));
            Assert.NotNull(mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path)));

            var journal = new RocksDbNodeReverseDiffStore(mgr, buildKeyMajorIndex: true);
            var stats = journal.MaterializingRewindTo(2);
            Assert.True(stats.EntriesApplied > 0, "test setup: expected the rewind to actually replay entries");

            Assert.Null(mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path)));

            Assert.Equal(controlBlob, mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(controlOwner, controlPath)));
        }

        [Fact]
        public async Task DeleteThenWipeInWindow_RewindRestoresSlotAbsent_NotStalePreDeleteValue()
        {
            var dir = SubDir("delete-then-wipe");
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0xA4 });
            var witnessOwner = _keccak.CalculateHash(new byte[] { 0xA5 });

            bundle.NodeCommitBlockSource.Arm(1);
            var storage = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage.Put(K0, V(1));
            storage.SaveDirtyNodesToStorage();
            var witnessStorage = new PatriciaTrie(bundle.StateTrieNodes, witnessOwner, _hp) { Tracer = new TrieTracer() };
            witnessStorage.Put(K0, V(7));
            witnessStorage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, Hash32(1)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var path = SoleOwnerPath(mgr, owner);
            var witnessPath = SoleOwnerPath(mgr, witnessOwner);
            var witnessV0Blob = mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(witnessOwner, witnessPath));

            bundle.NodeCommitBlockSource.Arm(2);
            storage.Delete(K0);
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            witnessStorage.Put(K0, V(77));
            witnessStorage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 3, Hash32(3)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            Assert.Empty(DumpOwnerRows(mgr, owner));
            var witnessAfterOverwrite = mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(witnessOwner, witnessPath));
            Assert.NotEqual(witnessV0Blob, witnessAfterOverwrite);

            var journal = new RocksDbNodeReverseDiffStore(mgr, buildKeyMajorIndex: true);
            var stats = journal.MaterializingRewindTo(2);
            Assert.True(stats.EntriesApplied > 0, "test setup: expected the rewind to actually replay entries");

            Assert.Null(mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path)));
            Assert.Equal(witnessV0Blob, mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(witnessOwner, witnessPath)));
        }
    }
}
