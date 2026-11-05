using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class InMemoryChainStoreBundleResetTests
    {
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

            await bundle.ResetSnapBootstrapStateAsync();

            Assert.True(bundle.Metadata.GetSnapSyncState() is null || bundle.Metadata.GetSnapSyncState().Phase == SnapPhase.NotStarted);
            Assert.Null(bundle.Metadata.GetDeferredHealAccountsBlob());
        }
    }
}
