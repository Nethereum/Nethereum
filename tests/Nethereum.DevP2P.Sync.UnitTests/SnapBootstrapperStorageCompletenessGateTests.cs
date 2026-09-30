using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperStorageCompletenessGateTests
    {
        [Fact]
        public void Given_NoDeferredStorageMetadata_When_ReportBuilt_Then_FinalizeAllowed()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var report = SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);

            Assert.True(report.CanFinalize);
            Assert.False(report.DeferredHealBlobPresent);
            Assert.Equal(0UL, report.OpenDeferredStorageDebts);
        }

        [Fact]
        public void Given_DeferredHealBlob_When_ReportBuilt_Then_FinalizeBlockedBeforeReconcile()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.SaveDeferredHealAccountsBlob(DeferredHealAccountsCodec.Encode(new[]
            {
                new SnapSyncClient.AccountNeedingHeal(Hash32(0x11), Hash32(0x22))
            }));

            var report = SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);

            Assert.False(report.CanFinalize);
            Assert.True(report.DeferredHealBlobPresent);
            Assert.Equal(1, report.PendingDeferredHealAccounts);
        }

        [Theory]
        [InlineData(StorageCompleteness.DeferredBigAccount, 1, 0, 0)]
        [InlineData(StorageCompleteness.DeferredUnavailable, 1, 0, 0)]
        [InlineData(StorageCompleteness.FinalRootReResolved, 0, 1, 0)]
        [InlineData(StorageCompleteness.DeepHealInProgress, 0, 1, 0)]
        [InlineData(StorageCompleteness.Damaged, 0, 0, 1)]
        public void Given_OpenStorageDebt_When_ReportBuilt_Then_FinalizeBlocked(
            StorageCompleteness status,
            int unresolvedBig,
            int finalNotDeep,
            int damaged)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var debt = Debt(status);
            if (status == StorageCompleteness.FinalRootReResolved || status == StorageCompleteness.DeepHealInProgress)
            {
                debt.FinalStateRoot = Hash32(0x44);
                debt.FinalStorageRoot = Hash32(0x55);
            }
            bundle.Metadata.UpsertDeferredStorageDebt(debt);

            var report = SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);

            Assert.False(report.CanFinalize);
            Assert.Equal(1UL, report.OpenDeferredStorageDebts);
            Assert.Equal(unresolvedBig, report.UnresolvedBigAccounts);
            Assert.Equal(finalNotDeep, report.FinalRootStorageNotDeepComplete);
            Assert.Equal(damaged, report.DamagedStorageInventories);
        }

        [Theory]
        [InlineData(StorageCompleteness.FullRangeFetched)]
        [InlineData(StorageCompleteness.DeepComplete)]
        [InlineData(StorageCompleteness.ProofDropped)]
        public void Given_TerminalStorageDebt_When_ReportBuilt_Then_FinalizeAllowed(StorageCompleteness status)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.UpsertDeferredStorageDebt(Debt(status));

            var report = SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);

            Assert.True(report.CanFinalize);
            Assert.Equal(0UL, report.OpenDeferredStorageDebts);
        }

        private static DeferredStorageDebt Debt(StorageCompleteness status)
            => new DeferredStorageDebt
            {
                AccountHash = Hash32(0x11),
                DiscoveredStorageRoot = Hash32(0x22),
                FetchStateRoot = Hash32(0x33),
                FetchPivotBlock = 123UL,
                Reason = DeferredStorageReason.BigAccountSubrangeFailed,
                Status = status,
            };

        private static byte[] Hash32(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
