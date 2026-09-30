using System;
using System.Diagnostics;
using Nethereum.DevP2P.Sync.FullSync;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerFailureTrackerTests
    {
        private const int Threshold = 3;
        private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan Bench = TimeSpan.FromSeconds(30);
        private static readonly long Freq = Stopwatch.Frequency;

        private static PeerFailureTracker NewTracker(Func<long> clock) =>
            new PeerFailureTracker(Threshold, Cooldown, Bench, clock);

        [Fact]
        public void Given_ConsecutiveTimeouts_When_ThresholdReached_Then_PeerIsBenchedNotDisposed()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, wasTimeout: true));
            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, wasTimeout: true));
            Assert.Equal(PeerFailureOutcome.Bench, t.RecordFailure(peer, wasTimeout: true));

            Assert.True(t.IsOnCooldown(peer));
            now += 2 * Freq;
            Assert.True(t.IsOnCooldown(peer));
            now += 30 * Freq;
            Assert.False(t.IsOnCooldown(peer));
        }

        [Fact]
        public void Given_APeerBenchedForTimeouts_When_AnInFlightStragglerAlsoTimesOut_Then_TheLongBenchIsNotClobbered()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            t.RecordFailure(peer, true);
            t.RecordFailure(peer, true);
            Assert.Equal(PeerFailureOutcome.Bench, t.RecordFailure(peer, true));
            Assert.Equal(PeerFailureOutcome.Bench, t.RecordFailure(peer, true));

            now += 2 * Freq;
            Assert.True(t.IsOnCooldown(peer));
        }

        [Fact]
        public void Given_APeerBenchedForTimeouts_When_ItThenThrowsATransportError_Then_TheDeadSocketIsDisposed()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            t.RecordFailure(peer, true);
            t.RecordFailure(peer, true);
            Assert.Equal(PeerFailureOutcome.Bench, t.RecordFailure(peer, true));
            Assert.Equal(PeerFailureOutcome.Dispose, t.RecordFailure(peer, false));
        }

        [Fact]
        public void Given_ATransportStreak_When_FailuresContinuePastThreshold_Then_EachStillReportsDispose()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            t.RecordFailure(peer, false);
            t.RecordFailure(peer, false);
            Assert.Equal(PeerFailureOutcome.Dispose, t.RecordFailure(peer, false));
            Assert.Equal(PeerFailureOutcome.Dispose, t.RecordFailure(peer, false));
        }

        [Fact]
        public void Given_AMixedStreak_When_ItTipsTheThreshold_Then_TheTippingFailureTypeDecidesTheOutcome()
        {
            long now = 0;

            var disposeOnTransportTip = NewTracker(() => now);
            var a = Guid.NewGuid();
            disposeOnTransportTip.RecordFailure(a, true);
            disposeOnTransportTip.RecordFailure(a, true);
            Assert.Equal(PeerFailureOutcome.Dispose, disposeOnTransportTip.RecordFailure(a, false));

            var benchOnTimeoutTip = NewTracker(() => now);
            var b = Guid.NewGuid();
            benchOnTimeoutTip.RecordFailure(b, false);
            benchOnTimeoutTip.RecordFailure(b, false);
            Assert.Equal(PeerFailureOutcome.Bench, benchOnTimeoutTip.RecordFailure(b, true));
        }

        [Fact]
        public void Given_ABenchedPeer_When_Forgotten_Then_ItStartsClean()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            t.RecordFailure(peer, true);
            t.RecordFailure(peer, true);
            t.RecordFailure(peer, true);
            t.Forget(peer);

            Assert.False(t.IsOnCooldown(peer));
            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, true));
        }

        [Fact]
        public void Given_ConsecutiveTransportErrors_When_ThresholdReached_Then_PeerIsDisposed()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, wasTimeout: false));
            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, wasTimeout: false));
            Assert.Equal(PeerFailureOutcome.Dispose, t.RecordFailure(peer, wasTimeout: false));
        }

        [Fact]
        public void Given_AFailureStreak_When_TheNextRequestSucceeds_Then_TheStreakAndCooldownClear()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var peer = Guid.NewGuid();

            t.RecordFailure(peer, true);
            t.RecordFailure(peer, true);
            Assert.True(t.IsOnCooldown(peer));

            t.RecordSuccess(peer);
            Assert.False(t.IsOnCooldown(peer));

            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, true));
            Assert.Equal(PeerFailureOutcome.Cooldown, t.RecordFailure(peer, true));
            Assert.Equal(PeerFailureOutcome.Bench, t.RecordFailure(peer, true));
        }

        [Fact]
        public void Given_TwoPeers_When_OneFails_Then_TheOtherIsUnaffected()
        {
            long now = 0;
            var t = NewTracker(() => now);
            var slow = Guid.NewGuid();
            var healthy = Guid.NewGuid();

            t.RecordFailure(slow, true);
            t.RecordFailure(slow, true);
            t.RecordFailure(slow, true);

            Assert.True(t.IsOnCooldown(slow));
            Assert.False(t.IsOnCooldown(healthy));
        }
    }
}
