using System;
using System.IO;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    /// <summary>
    /// The SST flat sink must be indistinguishable from the point-put path at read time: identical
    /// value bytes, last-wins on duplicates (within a buffer and across ingested files), zero
    /// values dropped, and nothing visible until flushed — the durability contract checkpoints
    /// rely on.
    /// </summary>
    public class SnapFlatSstSinkTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sstsink_{Guid.NewGuid():N}");
        private readonly RocksDbManager _mgr;
        private readonly RocksDbStateStore _store;

        public SnapFlatSstSinkTests()
        {
            Directory.CreateDirectory(_dir);
            _mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            _store = new RocksDbStateStore(_mgr);
        }

        public void Dispose()
        {
            _mgr.Dispose();
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private string ScratchDir => Path.Combine(_dir, "sst-scratch");

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

        [Fact]
        public async Task IngestedRows_ReadBackIdenticalToPointPath()
        {
            var viaSst = Hash32(0x11);
            var viaPut = Hash32(0x22);
            var account = MakeAccount(5, 500);

            using (var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir))
            {
                await sink.SaveAccountByHashAsync(viaSst, account);
                await sink.SaveStorageByHashAsync(viaSst, Hash32(0x01), new byte[] { 0x0A });
                sink.Flush();
            }
            await _store.SaveAccountByHashAsync(viaPut, account);
            await _store.SaveStorageByHashAsync(viaPut, Hash32(0x01), new byte[] { 0x0A });

            // Byte-identical values under both paths — the encode-identity contract.
            Assert.Equal(
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, viaPut),
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, viaSst));
            var sstSlotKey = new byte[64]; var putSlotKey = new byte[64];
            Buffer.BlockCopy(viaSst, 0, sstSlotKey, 0, 32); Buffer.BlockCopy(Hash32(0x01), 0, sstSlotKey, 32, 32);
            Buffer.BlockCopy(viaPut, 0, putSlotKey, 0, 32); Buffer.BlockCopy(Hash32(0x01), 0, putSlotKey, 32, 32);
            Assert.Equal(
                _mgr.Get(RocksDbManager.CF_STATE_STORAGE, putSlotKey),
                _mgr.Get(RocksDbManager.CF_STATE_STORAGE, sstSlotKey));
        }

        [Fact]
        public async Task DuplicateKeys_LastWins_WithinBufferAndAcrossFlushes()
        {
            var key = Hash32(0x33);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            // Reference values via the point path on a different key (encode-identity proven above).
            var reference = Hash32(0xEE);

            // Within one buffer: two versions, later arrival must win.
            await sink.SaveAccountByHashAsync(key, MakeAccount(1, 100));
            await sink.SaveAccountByHashAsync(key, MakeAccount(2, 200));
            sink.Flush();
            await _store.SaveAccountByHashAsync(reference, MakeAccount(2, 200));
            Assert.Equal(
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, reference),
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));

            // Across ingested files: a later flush must shadow the earlier one.
            await sink.SaveAccountByHashAsync(key, MakeAccount(3, 300));
            sink.Flush();
            await _store.SaveAccountByHashAsync(reference, MakeAccount(3, 300));
            Assert.Equal(
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, reference),
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
        }

        [Fact]
        public async Task UnflushedRows_AreNotVisible_AndZeroValuesDropped()
        {
            var key = Hash32(0x44);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveAccountByHashAsync(key, MakeAccount(9, 900));
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));   // durability contract: memory-only until Flush

            await sink.SaveStorageByHashAsync(key, Hash32(0x02), Array.Empty<byte>());
            sink.Flush();
            var zeroSlotKey = new byte[64];
            Buffer.BlockCopy(key, 0, zeroSlotKey, 0, 32); Buffer.BlockCopy(Hash32(0x02), 0, zeroSlotKey, 32, 32);
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, zeroSlotKey));
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
        }

        [Fact]
        public async Task ThresholdFlush_IngestsAutomatically_AndCountsRows()
        {
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir, flushEntryThreshold: 8);
            for (byte i = 1; i <= 9; i++)
                await sink.SaveAccountByHashAsync(Hash32(i), MakeAccount(i, (ulong)(i * 10)));

            // Threshold (8) crossed → first file ingested without an explicit Flush.
            Assert.True(sink.RowsIngested >= 8, $"expected auto-ingest, rows={sink.RowsIngested}");
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, Hash32(1)));

            sink.Flush();
            Assert.Equal(9, sink.RowsIngested);
            for (byte i = 1; i <= 9; i++)
                Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, Hash32(i)));
        }
    }
}
