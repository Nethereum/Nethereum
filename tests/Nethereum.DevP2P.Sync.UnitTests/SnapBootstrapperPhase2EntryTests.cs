using Nethereum.DevP2P.Sync;
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
    public class SnapBootstrapperPhase2EntryTests
    {
        [Fact]
        public void BackfillOnly_DoesNotMarkPhase2Entry()
        {
            Assert.False(SnapBootstrapper.ShouldMarkPhase2Entry(skipPhase2: false, backfillOnly: true));
        }

        [Fact]
        public void HealResume_DoesNotMarkPhase2Entry()
        {
            Assert.False(SnapBootstrapper.ShouldMarkPhase2Entry(skipPhase2: true, backfillOnly: false));
        }

        [Fact]
        public void NormalRun_MarksPhase2Entry()
        {
            Assert.True(SnapBootstrapper.ShouldMarkPhase2Entry(skipPhase2: false, backfillOnly: false));
        }
    }
}
