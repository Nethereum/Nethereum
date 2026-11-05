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
    [Trait("Slice", "Flat")]
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

            var reference = Hash32(0xEE);

            await sink.SaveAccountByHashAsync(key, MakeAccount(1, 100));
            await sink.SaveAccountByHashAsync(key, MakeAccount(2, 200));
            sink.Flush();
            await _store.SaveAccountByHashAsync(reference, MakeAccount(2, 200));
            Assert.Equal(
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, reference),
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));

            await sink.SaveAccountByHashAsync(key, MakeAccount(3, 300));
            sink.Flush();
            await _store.SaveAccountByHashAsync(reference, MakeAccount(3, 300));
            Assert.Equal(
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, reference),
                _mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
        }

        [Fact]
        public async Task UnflushedRows_AreNotVisible_AndAClearedSlotNeverWrittenStaysAbsent()
        {
            var key = Hash32(0x44);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveAccountByHashAsync(key, MakeAccount(9, 900));
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));

            await sink.SaveStorageByHashAsync(key, Hash32(0x02), Array.Empty<byte>());
            sink.Flush();
            var zeroSlotKey = new byte[64];
            Buffer.BlockCopy(key, 0, zeroSlotKey, 0, 32); Buffer.BlockCopy(Hash32(0x02), 0, zeroSlotKey, 32, 32);
            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, zeroSlotKey));
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, key));
        }

        private byte[] SlotKey(byte[] accountHash, byte[] slotHash)
        {
            var key = new byte[64];
            Buffer.BlockCopy(accountHash, 0, key, 0, 32);
            Buffer.BlockCopy(slotHash, 0, key, 32, 32);
            return key;
        }

        [Fact]
        public async Task Given_ASlotAlreadyIngestedWithANonZeroValue_When_ItIsCleared_Then_TheStaleRowIsGoneAfterFlush()
        {
            var accountHash = Hash32(0x51);
            var slotHash = Hash32(0x05);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveStorageByHashAsync(accountHash, slotHash, new byte[] { 0x0A });
            sink.Flush();
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, SlotKey(accountHash, slotHash)));

            await sink.SaveStorageByHashAsync(accountHash, slotHash, Array.Empty<byte>());
            sink.Flush();

            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, SlotKey(accountHash, slotHash)));
        }

        [Fact]
        public async Task Given_ASlotAlreadyIngestedWithANonZeroValue_When_AnotherNonZeroValueArrives_Then_TheRowIsUpdatedNotRemoved()
        {
            var accountHash = Hash32(0x52);
            var slotHash = Hash32(0x06);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveStorageByHashAsync(accountHash, slotHash, new byte[] { 0x0A });
            sink.Flush();
            await sink.SaveStorageByHashAsync(accountHash, slotHash, new byte[] { 0x0B });
            sink.Flush();

            Assert.Equal(new byte[] { 0x0B }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, SlotKey(accountHash, slotHash)));
        }

        [Fact]
        public async Task Given_ASlotWrittenThenClearedInTheSameBuffer_When_Flushed_Then_NoRowIsIngested()
        {
            var accountHash = Hash32(0x53);
            var slotHash = Hash32(0x07);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveStorageByHashAsync(accountHash, slotHash, new byte[] { 0x0A });
            await sink.SaveStorageByHashAsync(accountHash, slotHash, Array.Empty<byte>());
            sink.Flush();

            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_STORAGE, SlotKey(accountHash, slotHash)));
        }

        [Fact]
        public async Task Given_ASlotClearedThenWrittenInTheSameBuffer_When_Flushed_Then_TheLaterValueWins()
        {
            var accountHash = Hash32(0x54);
            var slotHash = Hash32(0x08);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveStorageByHashAsync(accountHash, slotHash, Array.Empty<byte>());
            await sink.SaveStorageByHashAsync(accountHash, slotHash, new byte[] { 0x0C });
            sink.Flush();

            Assert.Equal(new byte[] { 0x0C }, _mgr.Get(RocksDbManager.CF_STATE_STORAGE, SlotKey(accountHash, slotHash)));
        }

        [Fact]
        public async Task Given_AnAccountAlreadyIngested_When_ItIsDeletedByHash_Then_TheRowIsGoneAfterFlush()
        {
            var accountHash = Hash32(0x55);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveAccountByHashAsync(accountHash, MakeAccount(4, 400));
            sink.Flush();
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash));

            await sink.DeleteAccountByHashAsync(accountHash);
            sink.Flush();

            Assert.Null(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash));
        }

        [Fact]
        public async Task Given_AnAccountAlreadyIngested_When_ItIsSavedAgain_Then_TheRowSurvives()
        {
            var accountHash = Hash32(0x56);
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await sink.SaveAccountByHashAsync(accountHash, MakeAccount(4, 400));
            sink.Flush();
            await sink.SaveAccountByHashAsync(accountHash, MakeAccount(5, 500));
            sink.Flush();

            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash));
        }

        [Fact]
        public async Task Given_TheBulkSink_When_AFlatAccountIsReadBack_Then_ItRefusesLoudlyRatherThanReturningNothing()
        {
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir);

            await Assert.ThrowsAsync<NotSupportedException>(() => sink.GetAccountByHashAsync(Hash32(0x57)));
        }

        [Fact]
        public async Task ThresholdFlush_IngestsAutomatically_AndCountsRows()
        {
            using var sink = new SnapFlatSstSink(_mgr, _store, ScratchDir, flushEntryThreshold: 8);
            for (byte i = 1; i <= 9; i++)
                await sink.SaveAccountByHashAsync(Hash32(i), MakeAccount(i, (ulong)(i * 10)));

            Assert.True(sink.RowsIngested >= 8, $"expected auto-ingest, rows={sink.RowsIngested}");
            Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, Hash32(1)));

            sink.Flush();
            Assert.Equal(9, sink.RowsIngested);
            for (byte i = 1; i <= 9; i++)
                Assert.NotNull(_mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, Hash32(i)));
        }
    }
}
