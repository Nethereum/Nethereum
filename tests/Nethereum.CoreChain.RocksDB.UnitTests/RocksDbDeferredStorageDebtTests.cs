using System;
using System.IO;
using Nethereum.Util;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Xunit;

#pragma warning disable CS0618

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RocksDbDeferredStorageDebtTests : IDisposable
    {
        private readonly string _dbPath;
        private RocksDbManager _manager;
        private RocksDbChainMetadataStore _store;

        public RocksDbDeferredStorageDebtTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rocksdb_deferred_storage_debt_{Guid.NewGuid():N}");
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _store = new RocksDbChainMetadataStore(_manager);
        }

        public void Dispose()
        {
            _manager?.Dispose();
            if (Directory.Exists(_dbPath))
            {
                try { Directory.Delete(_dbPath, true); }
                catch { }
            }
        }

        [Fact]
        public void Given_OpenDeferredStorageDebtSaved_When_Reopened_Then_DebtSurvives()
        {
            var debt = Debt(StorageCompleteness.DeferredBigAccount);

            SeedRow(debt);
            Reopen();

            Assert.Equal(1UL, _store.CountOpenDeferredStorageDebts());
            var read = Assert.Single(_store.ListOpenDeferredStorageDebts());
            Assert.Equal(debt.AccountHash, read.AccountHash);
            Assert.Equal(debt.DiscoveredStorageRoot, read.DiscoveredStorageRoot);
            Assert.Equal(debt.FetchStateRoot, read.FetchStateRoot);
            Assert.Equal(debt.FetchPivotBlock, read.FetchPivotBlock);
            Assert.Equal(debt.Reason, read.Reason);
            Assert.Equal(debt.Status, read.Status);
        }

        [Theory]
        [InlineData(StorageCompleteness.DeferredBigAccount, true)]
        [InlineData(StorageCompleteness.DeferredUnavailable, true)]
        [InlineData(StorageCompleteness.FinalRootReResolved, true)]
        [InlineData(StorageCompleteness.DeepHealInProgress, true)]
        [InlineData(StorageCompleteness.Damaged, true)]
        [InlineData(StorageCompleteness.FullRangeFetched, false)]
        [InlineData(StorageCompleteness.DeepComplete, false)]
        [InlineData(StorageCompleteness.ProofDropped, false)]
        public void Given_DebtStatus_When_CountOpen_Then_OnlyBlockingStatusesCount(
            StorageCompleteness status,
            bool expectedOpen)
        {
            SeedRow(Debt(status));

            Assert.Equal(expectedOpen ? 1UL : 0UL, _store.CountOpenDeferredStorageDebts());
            Assert.Equal(expectedOpen ? 1 : 0, _store.ListOpenDeferredStorageDebts().Count);
        }

        [Fact]
        public void Given_DebtSaved_When_Cleared_Then_OpenDebtIsRemoved()
        {
            var debt = Debt(StorageCompleteness.DeferredBigAccount);
            SeedRow(debt);

            _store.ClearDeferredStorageDebt(debt.AccountHash, debt.DiscoveredStorageRoot);

            Assert.Equal(0UL, _store.CountOpenDeferredStorageDebts());
            Assert.Empty(_store.ListOpenDeferredStorageDebts());
        }

        [Fact]
        public void Given_MaxZero_When_ListOpenDeferredStorageDebts_Then_ReturnsEmpty()
        {
            SeedRow(Debt(StorageCompleteness.DeferredBigAccount));

            Assert.Empty(_store.ListOpenDeferredStorageDebts(0));
        }

        [Fact]
        public void Given_MalformedDebtRow_When_CountOpen_Then_RowStillBlocksFinalization()
        {
            var accountHash = Hash(0x66);
            var storageRoot = Hash(0x77);
            _manager.Put(
                RocksDbManager.CF_METADATA,
                RocksDbChainMetadataStore.DeferredStorageDebtKey(accountHash, storageRoot),
                ByteUtil.Merge(new byte[] { 0xff }, accountHash));

            Assert.Equal(1UL, _store.CountOpenDeferredStorageDebts());
            Assert.Empty(_store.ListOpenDeferredStorageDebts());
        }
        [Fact]
        public void Given_AnUpsertedDebt_When_Counted_Then_NothingWasWritten()
        {
            _store.UpsertDeferredStorageDebt(Debt(StorageCompleteness.DeferredBigAccount));

            Assert.Equal(0UL, _store.CountOpenDeferredStorageDebts());
            Assert.Empty(_store.ListOpenDeferredStorageDebts());
        }

        private void SeedRow(DeferredStorageDebt debt)
            => _manager.Put(
                RocksDbManager.CF_METADATA,
                RocksDbChainMetadataStore.DeferredStorageDebtKey(debt.AccountHash, debt.DiscoveredStorageRoot),
                DeferredStorageDebtCodec.Encode(debt));

        private void Reopen()
        {
            _manager.Dispose();
            _manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = _dbPath });
            _store = new RocksDbChainMetadataStore(_manager);
        }

        private static DeferredStorageDebt Debt(StorageCompleteness status)
            => new DeferredStorageDebt
            {
                AccountHash = Hash(0x11),
                DiscoveredStorageRoot = Hash(0x22),
                FetchStateRoot = Hash(0x33),
                FetchPivotBlock = 25_000_000,
                Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                Status = status,
                FinalStateRoot = status == StorageCompleteness.FinalRootReResolved ? Hash(0x44) : null,
                FinalStorageRoot = status == StorageCompleteness.FinalRootReResolved ? Hash(0x55) : null,
            };

        private static byte[] Hash(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
