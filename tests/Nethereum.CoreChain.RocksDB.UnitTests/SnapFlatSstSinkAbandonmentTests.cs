using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class SnapFlatSstSinkAbandonmentTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sstabandon_{Guid.NewGuid():N}");
        private readonly RocksDbManager _mgr;
        private readonly RocksDbStateStore _store;
        private readonly RocksDbChainStoreBundle _bundle;

        public SnapFlatSstSinkAbandonmentTests()
        {
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _store = new RocksDbStateStore(_mgr);
            _bundle = RocksDbChainStoreBundle.FromManager(_mgr, _dir);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private static byte[] Hash32(byte fill)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        private static Account MakeAccount(ulong nonce, ulong balance)
            => new Account
            {
                Nonce = (EvmUInt256)nonce,
                Balance = (EvmUInt256)balance,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            };

        private async Task<byte[]> PointImageAsync(byte referenceFill, Account account)
        {
            var refKey = Hash32(referenceFill);
            await _store.SaveAccountByHashAsync(refKey, account);
            return _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, refKey);
        }

        [Fact]
        public void Abandon_DiscardsBufferedRows_NeverIngested()
        {
            var key = Hash32(0x51);
            var sink = _bundle.CreateBulkFlatSink();
            sink.SaveAccountByHashAsync(key, MakeAccount(1, 100)).GetAwaiter().GetResult();

            sink.Abandon();
            sink.Dispose();

            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
        }

        [Fact]
        public void SealedSink_FencesLateOrphanWrites()
        {
            var key = Hash32(0x52);
            var sink = _bundle.CreateBulkFlatSink();

            sink.Abandon();
            sink.SaveAccountByHashAsync(key, MakeAccount(2, 200)).GetAwaiter().GetResult();
            sink.SaveStorageByHashAsync(key, Hash32(0x01), new byte[] { 0x0B }).GetAwaiter().GetResult();
            sink.Flush();
            sink.Dispose();

            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
            var slotKey = new byte[64];
            Buffer.BlockCopy(key, 0, slotKey, 0, 32); Buffer.BlockCopy(Hash32(0x01), 0, slotKey, 32, 32);
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, slotKey));
        }

        [Fact]
        public async Task InFlightStaleIngest_CannotShadowRecycledAttempt_AbandonWaitsForIt()
        {
            var key = Hash32(0x53);
            var correctImage = await PointImageAsync(0xE2, MakeAccount(9, 999));
            var staleImage = await PointImageAsync(0xE1, MakeAccount(1, 100));
            Assert.NotEqual(staleImage, correctImage);

            var sinkA = (SnapFlatSstSink)_bundle.CreateBulkFlatSink();

            var pausedInside = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseIngest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            sinkA.PreIngestHook = () =>
            {
                pausedInside.TrySetResult(true);
                releaseIngest.Task.GetAwaiter().GetResult();
            };

            await sinkA.SaveAccountByHashAsync(key, MakeAccount(1, 100));
            var staleIngest = Task.Run(() => sinkA.Flush());
            await pausedInside.Task;

            var abandon = Task.Run(() => sinkA.Abandon());
            await Task.Delay(250);
            Assert.False(abandon.IsCompleted,
                "Abandon() returned while a stale ingest was still in-flight — the recycled attempt could start and be shadowed");

            releaseIngest.TrySetResult(true);
            await staleIngest;
            await abandon;
            sinkA.Dispose();

            var sinkB = _bundle.CreateBulkFlatSink();
            await sinkB.SaveAccountByHashAsync(key, MakeAccount(9, 999));
            sinkB.Flush();
            sinkB.Dispose();

            Assert.Equal(correctImage, _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
        }

        [Fact]
        public void PerAttemptScratch_IsIsolated_NewAttemptDoesNotClearAnorphansScratch()
        {
            var sinkA = (SnapFlatSstSink)_bundle.CreateBulkFlatSink();
            var sinkB = (SnapFlatSstSink)_bundle.CreateBulkFlatSink();

            Assert.NotEqual(sinkA.ScratchDirectory, sinkB.ScratchDirectory);

            var inflight = Path.Combine(sinkA.ScratchDirectory, "inflight_orphan.sst");
            File.WriteAllText(inflight, "in-flight ingest data");

            var sinkC = (SnapFlatSstSink)_bundle.CreateBulkFlatSink();
            Assert.NotEqual(sinkA.ScratchDirectory, sinkC.ScratchDirectory);
            Assert.True(File.Exists(inflight), "a new attempt's construction deleted an orphan's in-flight scratch file");

            sinkA.Abandon(); sinkA.Dispose();
            sinkB.Dispose();
            sinkC.Dispose();
        }

        [Fact]
        public void AbandonedSink_CleansItsOwnScratchDir_OnDispose()
        {
            var sink = (SnapFlatSstSink)_bundle.CreateBulkFlatSink();
            var scratch = sink.ScratchDirectory;
            Assert.True(Directory.Exists(scratch));

            sink.Abandon();
            sink.Dispose();

            Assert.False(Directory.Exists(scratch));
        }
    }
}
