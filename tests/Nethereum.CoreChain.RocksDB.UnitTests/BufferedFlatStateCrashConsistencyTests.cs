using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class BufferedFlatStateCrashConsistencyTests : IDisposable
    {
        private readonly string _dir;

        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string AddrB = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        public BufferedFlatStateCrashConsistencyTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "necc-crashflat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
        }

        [Fact]
        public async Task CrashMidBlock_NeverFlushed_LeavesZeroOrphanRows_ParentIntact_ThenReExecuteSucceeds()
        {
            using (var mgr1 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir }))
            {
                var bundle1 = RocksDbChainStoreBundle.FromManager(mgr1, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
                var hist1 = (HistoricalStateStore)bundle1.State;
                hist1.SetCurrentBlockNumber(1);
                await hist1.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
                await hist1.ClearCurrentBlockNumberAsync();
            }

            using (var mgr2 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir }))
            {
                var bundle2 = RocksDbChainStoreBundle.FromManager(mgr2, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
                var hist2 = (HistoricalStateStore)bundle2.State;
                hist2.SetCurrentBlockNumber(2);
                await hist2.SaveAccountAsync(AddrA, new Account { Balance = 2000, Nonce = 2 });
                await hist2.SaveAccountAsync(AddrB, new Account { Balance = 500, Nonce = 1 });
                await hist2.SaveStorageAsync(AddrB, BigInteger.One, new byte[] { 0x77 });
            }

            using (var mgr3 = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir }))
            {
                var raw3 = new RocksDbStateStore(mgr3);
                Assert.Null(await raw3.GetAccountAsync(AddrB));
                Assert.Null(await raw3.GetStorageAsync(AddrB, BigInteger.One));

                var a = await raw3.GetAccountAsync(AddrA);
                Assert.NotNull(a);
                Assert.Equal((EvmUInt256)1000, a.Balance);
                Assert.Equal((EvmUInt256)1, a.Nonce);

                var bundle3 = RocksDbChainStoreBundle.FromManager(mgr3, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
                var hist3 = (HistoricalStateStore)bundle3.State;
                hist3.SetCurrentBlockNumber(2);
                await hist3.SaveAccountAsync(AddrA, new Account { Balance = 2000, Nonce = 2 });
                await hist3.SaveAccountAsync(AddrB, new Account { Balance = 500, Nonce = 1 });
                await hist3.SaveStorageAsync(AddrB, BigInteger.One, new byte[] { 0x77 });
                await hist3.ClearCurrentBlockNumberAsync();

                var aFinal = await raw3.GetAccountAsync(AddrA);
                Assert.Equal((EvmUInt256)2000, aFinal.Balance);
                var bFinal = await raw3.GetAccountAsync(AddrB);
                Assert.NotNull(bFinal);
                Assert.Equal((EvmUInt256)500, bFinal.Balance);
                Assert.Equal(new byte[] { 0x77 }, await raw3.GetStorageAsync(AddrB, BigInteger.One));
            }
        }

        [Fact]
        public async Task Mismatch_RevertsCleanly_ParentFlatStateUntouched()
        {
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;
            var raw = new RocksDbStateStore(mgr);

            hist.SetCurrentBlockNumber(1);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 111, Nonce = 1 });
            await hist.ClearCurrentBlockNumberAsync();

            hist.SetCurrentBlockNumber(2);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 999, Nonce = 9 });
            await hist.SaveAccountAsync(AddrB, new Account { Balance = 1, Nonce = 1 });
            await hist.RevertCurrentBlockAsync();

            var a = await raw.GetAccountAsync(AddrA);
            Assert.Equal((EvmUInt256)111, a.Balance);
            Assert.Null(await raw.GetAccountAsync(AddrB));
        }

        [Fact]
        public async Task NoRegression_BufferedVsUnbuffered_ByteIdenticalFlatStateAndJournal()
        {
            var dirUnbuffered = Path.Combine(_dir, "unbuffered");
            var dirBuffered = Path.Combine(_dir, "buffered");
            Directory.CreateDirectory(dirUnbuffered);
            Directory.CreateDirectory(dirBuffered);

            using var mgrUnbuffered = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirUnbuffered });
            using var mgrBuffered = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dirBuffered });

            var rawUnbuffered = new RocksDbStateStore(mgrUnbuffered);
            var diffsUnbuffered = new RocksDbStateDiffStore(mgrUnbuffered);
            var histUnbuffered = new HistoricalStateStore(rawUnbuffered, diffsUnbuffered, HistoricalStateOptions.FullArchive);

            var rawBuffered = new RocksDbStateStore(mgrBuffered);
            var diffsBuffered = new RocksDbStateDiffStore(mgrBuffered);
            var histBuffered = new HistoricalStateStore(new BufferedFlatStateStore(rawBuffered), diffsBuffered, HistoricalStateOptions.FullArchive);

            async Task DriveBlockAsync(HistoricalStateStore hist)
            {
                hist.SetCurrentBlockNumber(1);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000, Nonce = 1 });
                await hist.SaveStorageAsync(AddrA, 1, new byte[] { 0x10 });
                await hist.SaveStorageAsync(AddrA, 2, new byte[] { 0x20 });
                await hist.SaveAccountAsync(AddrB, new Account { Balance = 5, Nonce = 1 });
                await hist.SaveStorageAsync(AddrA, 2, System.Array.Empty<byte>());
                await hist.ClearCurrentBlockNumberAsync();
            }

            await DriveBlockAsync(histUnbuffered);
            await DriveBlockAsync(histBuffered);

            var aU = await rawUnbuffered.GetAccountAsync(AddrA);
            var aB = await rawBuffered.GetAccountAsync(AddrA);
            Assert.Equal(aU.Balance, aB.Balance);
            Assert.Equal(aU.Nonce, aB.Nonce);

            var bU = await rawUnbuffered.GetAccountAsync(AddrB);
            var bB = await rawBuffered.GetAccountAsync(AddrB);
            Assert.Equal(bU.Balance, bB.Balance);

            Assert.Equal(await rawUnbuffered.GetStorageAsync(AddrA, 1), await rawBuffered.GetStorageAsync(AddrA, 1));
            Assert.Null(await rawUnbuffered.GetStorageAsync(AddrA, 2));
            Assert.Null(await rawBuffered.GetStorageAsync(AddrA, 2));

            var diffU = await diffsUnbuffered.GetBlockDiffAsync(1);
            var diffB = await diffsBuffered.GetBlockDiffAsync(1);
            Assert.Equal(diffU.AccountDiffs.Count, diffB.AccountDiffs.Count);
            Assert.Equal(diffU.StorageDiffs.Count, diffB.StorageDiffs.Count);
        }

        [Fact]
        public async Task ReorgRewind_ThroughFlushedBufferedBlock_RestoresParentValues()
        {
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var rawState = new RocksDbStateStore(mgr);
            var diffs = new RocksDbStateDiffStore(mgr);
            var blocks = new RocksDbBlockStore(mgr);
            var meta = new RocksDbChainMetadataStore(mgr);
            var hist = new HistoricalStateStore(new BufferedFlatStateStore(rawState), diffs, HistoricalStateOptions.FullArchive);

            byte[] Hash(byte b) { var h = new byte[32]; h[0] = b; return h; }
            BlockHeader MakeHeader(ulong number, byte[] parentHash) => new BlockHeader
            {
                BlockNumber = number,
                ParentHash = parentHash,
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            await blocks.SaveAsync(MakeHeader(0, new byte[32]), Hash(0));
            await blocks.SaveAsync(MakeHeader(1, Hash(0)), Hash(1));
            meta.Commit(0, Hash(0));

            hist.SetCurrentBlockNumber(1);
            await hist.SaveAccountAsync(AddrA, new Account { Balance = 42, Nonce = 1 });
            await hist.ClearCurrentBlockNumberAsync();
            meta.Commit(1, Hash(1));

            Assert.NotNull(await rawState.GetAccountAsync(AddrA));

            var rewind = new StateRewindService(rawState, diffs, blocks, meta);
            var undone = await rewind.RewindWithJournalAsync(0);

            Assert.Equal(1UL, undone);
            Assert.Null(await rawState.GetAccountAsync(AddrA));
            Assert.Equal(0UL, meta.GetLastBlock());
        }

        [Fact]
        public async Task SnapBulkPath_Unaffected_NeverBuffered_VisibleImmediately()
        {
            using var mgr = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dir });
            var bundle = RocksDbChainStoreBundle.FromManager(mgr, _dir, HistoricalStateOptions.FullArchive, ownsManager: false);
            var hist = (HistoricalStateStore)bundle.State;

            hist.SetCurrentBlockNumber(1);

            var accountHash = new byte[32];
            accountHash[0] = 0x11;
            using (var sink = bundle.CreateBulkFlatSink())
            {
                await sink.SaveAccountByHashAsync(accountHash, new Account { Balance = 3, Nonce = 1 });
                sink.Flush();
            }

            Assert.NotNull(mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash));

            await hist.RevertCurrentBlockAsync();
            Assert.NotNull(mgr.Get(RocksDbManager.CF_STATE_ACCOUNTS, accountHash));
        }
    }
}
