using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class InMemoryDeferredStorageDebtTests
    {
        [Fact]
        public async Task Given_DebtStagedInBundleBatch_When_Committed_Then_DebtAndCursorBecomeVisibleTogether()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            using (var batch = bundle.BeginBatch())
            {
                batch.UpsertDeferredStorageDebt(Debt(StorageCompleteness.DeferredBigAccount));
                batch.SetLastFetchedHeaderAndBody(100, 100);
                await batch.CommitAsync();
            }

            Assert.Equal(100UL, bundle.Metadata.GetLastFetchedHeader());
            Assert.Equal(100UL, bundle.Metadata.GetLastFetchedBody());
            Assert.Equal(1UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Single(bundle.Metadata.ListOpenDeferredStorageDebts());
        }

        [Fact]
        public void Given_MaxZero_When_ListOpenDeferredStorageDebts_Then_ReturnsEmpty()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(StorageCompleteness.DeferredBigAccount));

            Assert.Empty(bundle.Metadata.ListOpenDeferredStorageDebts(0));
        }

        [Fact]
        public async Task Given_SnapRepairMetadata_When_ResetSnapBootstrapState_Then_RepairMetadataIsCleared()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase2Running,
                PivotBlockNumber = 0,
                PivotBlockHash = new byte[32],
                HealTargetRoot = new byte[32],
                Tasks = System.Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });
            bundle.Metadata.SaveDeferredHealAccountsBlob(new byte[] { 1, 2, 3 });
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(StorageCompleteness.DeferredBigAccount));

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.True(bundle.Metadata.GetSnapSyncState() is null || bundle.Metadata.GetSnapSyncState().Phase == SnapPhase.NotStarted);
            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
            Assert.Equal(0UL, bundle.Metadata.CountOpenDeferredStorageDebts());
            Assert.Empty(bundle.Metadata.ListOpenDeferredStorageDebts());
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
            };

        private static byte[] Hash(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
