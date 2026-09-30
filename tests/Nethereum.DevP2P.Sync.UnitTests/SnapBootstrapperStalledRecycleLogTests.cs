using System;
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
    public class SnapBootstrapperStalledRecycleLogTests
    {
        private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(10);

        [Fact]
        public void FirstObservation_WithNoPriorLog_LogsImmediately()
        {
            var now = DateTimeOffset.UtcNow;
            Assert.True(SnapBootstrapper.ShouldLogStalledRecycle(lastLoggedAt: null, now, MinInterval));
        }

        [Fact]
        public void BeforeMinIntervalElapsed_DoesNotLogAgain()
        {
            var lastLoggedAt = DateTimeOffset.UtcNow;
            var now = lastLoggedAt + TimeSpan.FromMinutes(5);
            Assert.False(SnapBootstrapper.ShouldLogStalledRecycle(lastLoggedAt, now, MinInterval));
        }

        [Fact]
        public void AtOrAfterMinIntervalElapsed_LogsAgain()
        {
            var lastLoggedAt = DateTimeOffset.UtcNow;
            var atInterval = lastLoggedAt + MinInterval;
            var pastInterval = lastLoggedAt + MinInterval + TimeSpan.FromSeconds(1);

            Assert.True(SnapBootstrapper.ShouldLogStalledRecycle(lastLoggedAt, atInterval, MinInterval));
            Assert.True(SnapBootstrapper.ShouldLogStalledRecycle(lastLoggedAt, pastInterval, MinInterval));
        }
    }
}
