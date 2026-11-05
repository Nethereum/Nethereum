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
            bundle.Metadata.SaveDeferredHealAccountsBlob(NeedsHealAccountsCodec.Encode(new[]
            {
                new SnapSyncClient.AccountNeedingHeal(Hash32(0x11), Hash32(0x22))
            }));

            var report = SnapBootstrapper.BuildStorageCompletenessGateReport(bundle);

            Assert.False(report.CanFinalize);
            Assert.True(report.DeferredHealBlobPresent);
            Assert.Equal(1, report.PendingDeferredHealAccounts);
        }

        private static byte[] Hash32(byte value)
        {
            var hash = new byte[32];
            hash[31] = value;
            return hash;
        }
    }
}
