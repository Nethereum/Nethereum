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
    public class WipeBarrierWindowTests : IDisposable
    {
        private readonly string _dir;
        private static readonly Sha3KeccackHashProvider _hp = Sha3KeccackHashProvider.Instance;
        private static readonly Sha3Keccack _keccak = new();

        public WipeBarrierWindowTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-wipebarrier-window-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        private static RocksDbStorageOptions PathKeyedOptions(string dataDir) => new RocksDbStorageOptions
        {
            DatabasePath = dataDir,
            PathKeyedState = true,
            TrieNodeHistoryBlocks = 100_000,
            TrieNodeHistoryIndex = true,
        };

        private static byte[] Hash32(byte b) { var h = new byte[32]; for (int i = 0; i < 32; i++) h[i] = (byte)(b + i); return h; }
        private static readonly byte[] K0 = _keccak.CalculateHash(new byte[] { 0x9C });
        private static byte[] V(int i) => Nethereum.RLP.RLP.EncodeElement(new byte[] { (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i, (byte)i });

        private static byte[] BlockKey(ulong block, byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            var pathLen = path?.Length ?? 0;
            var ownerLen = acct ? 0 : owner.Length;
            var key = new byte[8 + 1 + ownerLen + pathLen];
            var blockBe = RocksDbManager.Write64BE(block);
            Buffer.BlockCopy(blockBe, 0, key, 0, 8);
            int o = 8;
            key[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, key, o, ownerLen); o += ownerLen; }
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, o, pathLen);
            return key;
        }

        private static byte[] IndexKey(ulong block, byte[] owner, byte[] path)
        {
            bool acct = owner == null || owner.Length == 0;
            var ownerLen = acct ? 0 : owner.Length;
            var pathLen = path?.Length ?? 0;
            var prefix = new byte[1 + ownerLen + 1 + pathLen];
            int o = 0;
            prefix[o++] = (byte)(acct ? 'A' : 'O');
            if (ownerLen > 0) { Buffer.BlockCopy(owner, 0, prefix, o, ownerLen); o += ownerLen; }
            prefix[o++] = (byte)pathLen;
            if (pathLen > 0) Buffer.BlockCopy(path, 0, prefix, o, pathLen);
            var key = new byte[prefix.Length + 8];
            Buffer.BlockCopy(prefix, 0, key, 0, prefix.Length);
            Buffer.BlockCopy(RocksDbManager.Write64BE(block), 0, key, prefix.Length, 8);
            return key;
        }

        private static async Task<byte[]> SeedBlock1Async(RocksDbManager mgr, RocksDbChainStoreBundle bundle, byte[] owner)
        {
            bundle.NodeCommitBlockSource.Arm(1);
            var storage = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage.Put(K0, V(1));
            storage.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();
            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 1, Hash32(1)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            using var it = mgr.CreateIterator(RocksDbManager.CF_STATE_TRIE_STORAGE);
            it.Seek(owner);
            Assert.True(it.Valid() && ByteUtil.StartsWith(it.Key(), owner), "test setup: expected the seeded single-key subtree to land on disk");
            var key = it.Key();
            var path = new byte[key.Length - owner.Length];
            Buffer.BlockCopy(key, owner.Length, path, 0, path.Length);
            return path;
        }

        [Fact]
        public async Task WipeThenRecreateInWindow_JournalsAbsentPreImage_NotStaleDiskBlob()
        {
            var dir = Path.Combine(_dir, "wipe-then-recreate");
            Directory.CreateDirectory(dir);
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0x51 });

            var path = await SeedBlock1Async(mgr, bundle, owner);
            var v0Blob = mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path));
            Assert.NotNull(v0Blob);

            bundle.NodeCommitBlockSource.Arm(2);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            var storage3 = new PatriciaTrie(bundle.StateTrieNodes, owner, _hp) { Tracer = new TrieTracer() };
            storage3.Put(K0, V(99));
            storage3.SaveDirtyNodesToStorage();
            bundle.NodeCommitBlockSource.Clear();

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 3, Hash32(3)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var recordBlock3 = mgr.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(3, owner, path));
            Assert.NotNull(recordBlock3);
            Assert.Empty(recordBlock3);
            Assert.NotEqual(v0Blob, recordBlock3);

            var indexBlock3 = mgr.Get(RocksDbManager.CF_NODE_HISTORY_INDEX, IndexKey(3, owner, path));
            Assert.NotNull(indexBlock3);
        }

        [Fact]
        public async Task DoubleWipeSameOwnerInWindow_SecondWipeSkipsStaleDiskRow_NotJournalsV0Again()
        {
            var dir = Path.Combine(_dir, "double-wipe");
            Directory.CreateDirectory(dir);
            using var mgr = new RocksDbManager(PathKeyedOptions(dir));
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, dir, ownsManager: false);
            var owner = _keccak.CalculateHash(new byte[] { 0x64 });

            var path = await SeedBlock1Async(mgr, bundle, owner);
            var v0Blob = mgr.Get(RocksDbManager.CF_STATE_TRIE_STORAGE, StorageKey(owner, path));
            Assert.NotNull(v0Blob);

            bundle.NodeCommitBlockSource.Arm(2);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            bundle.NodeCommitBlockSource.Clear();

            bundle.NodeCommitBlockSource.Arm(3);
            ((IContractStorageWipeable)bundle.StateTrieNodes).DeleteRange(owner);
            bundle.NodeCommitBlockSource.Clear();

            await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, 3, Hash32(3)).ConfigureAwait(false);
            await ((IAtomicBlockFlush)bundle).DrainAsync();

            var recordBlock2 = mgr.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(2, owner, path));
            var recordBlock3 = mgr.Get(RocksDbManager.CF_NODE_HISTORY, BlockKey(3, owner, path));

            Assert.NotNull(recordBlock2);
            Assert.Equal(v0Blob, recordBlock2);

            Assert.Null(recordBlock3);

            var indexBlock2 = mgr.Get(RocksDbManager.CF_NODE_HISTORY_INDEX, IndexKey(2, owner, path));
            var indexBlock3 = mgr.Get(RocksDbManager.CF_NODE_HISTORY_INDEX, IndexKey(3, owner, path));
            Assert.NotNull(indexBlock2);
            Assert.Null(indexBlock3);
        }

        private static byte[] StorageKey(byte[] owner, byte[] path)
        {
            var key = new byte[owner.Length + path.Length];
            Buffer.BlockCopy(owner, 0, key, 0, owner.Length);
            Buffer.BlockCopy(path, 0, key, owner.Length, path.Length);
            return key;
        }
    }
}
