using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Peering;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Peering
{
    public class DialSchedulerTests
    {
        private static DialCandidate Candidate(int index, bool trusted = false) =>
            new DialCandidate($"enode://peer-{index}", trusted);

        [Fact]
        public async Task Given_ManyConcurrentReserveReleaseCycles_When_AllComplete_Then_InFlightReturnsToZero()
        {
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 1000,
            }, maxPeers: 1_000_000);

            const int workers = 8;
            const int perWorker = 5000;

            var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(async () =>
            {
                for (int i = 0; i < perWorker; i++)
                {
                    var candidate = Candidate(w * perWorker + i);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    if (await scheduler.TryReserveSlotAsync(candidate, cts.Token))
                        scheduler.ReleaseSlot(candidate, DialOutcome.Success);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(0, scheduler.ActiveDialCount);
        }

        [Fact]
        public async Task Given_ConcurrentCap_When_OneExtraReserves_Then_BlocksUntilRelease()
        {
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 16,
            }, maxPeers: 1000);

            var firstWave = new DialCandidate[16];
            for (int i = 0; i < 16; i++) firstWave[i] = Candidate(i);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            foreach (var c in firstWave)
                Assert.True(await scheduler.TryReserveSlotAsync(c, cts.Token));

            Assert.Equal(16, scheduler.ActiveDialCount);

            var seventeenth = Candidate(16);
            var pending = scheduler.TryReserveSlotAsync(seventeenth, cts.Token);

            await Task.Delay(100);
            Assert.False(pending.IsCompleted,
                "17th TryReserveSlotAsync completed while all 16 slots were held.");

            scheduler.ReleaseSlot(firstWave[0], DialOutcome.Success);
            var seventeenthResult = await pending;
            Assert.True(seventeenthResult);
            Assert.Equal(16, scheduler.ActiveDialCount);

            for (int i = 1; i < 16; i++)
                scheduler.ReleaseSlot(firstWave[i], DialOutcome.Failure);
            scheduler.ReleaseSlot(seventeenth, DialOutcome.Failure);
            Assert.Equal(0, scheduler.ActiveDialCount);
        }

        [Fact]
        public async Task Given_RecentDialHistory_When_SameCandidateRetried_Then_SuppressedUntilExpiration()
        {
            var clock = new TestClock(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));
            var scheduler = new DialScheduler(
                new DialSchedulerOptions
                {
                    MaxActiveDials = 4,
                    DialHistoryExpiration = TimeSpan.FromMinutes(5),
                },
                maxPeers: 1000,
                now: clock.Now);

            var peer = Candidate(1);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            Assert.True(await scheduler.TryReserveSlotAsync(peer, cts.Token));
            scheduler.ReleaseSlot(peer, DialOutcome.Failure);

            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.False(await scheduler.TryReserveSlotAsync(peer, cts.Token));

            clock.Advance(TimeSpan.FromMinutes(3));
            Assert.False(await scheduler.TryReserveSlotAsync(peer, cts.Token));

            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.True(await scheduler.TryReserveSlotAsync(peer, cts.Token));
            scheduler.ReleaseSlot(peer, DialOutcome.Success);
        }

        [Fact]
        public async Task Given_RatioCap_When_OutboundCapHit_Then_RejectedButInboundStillCounted()
        {
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 100,
            }, maxPeers: 10);
            Assert.Equal(6, scheduler.OutboundCap);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            for (int i = 0; i < 5; i++)
            {
                var c = Candidate(i);
                Assert.True(await scheduler.TryReserveSlotAsync(c, cts.Token));
                scheduler.ReleaseSlot(c, DialOutcome.Success);
                scheduler.OnPeerConnected(c.Key, PeerDirection.Outbound);
            }
            Assert.Equal(5, scheduler.OutboundPeerCount);

            var sixth = Candidate(5);
            Assert.True(await scheduler.TryReserveSlotAsync(sixth, cts.Token));

            var seventh = Candidate(6);
            Assert.False(await scheduler.TryReserveSlotAsync(seventh, cts.Token));

            scheduler.OnPeerConnected("inbound-1", PeerDirection.Inbound);
            scheduler.OnPeerConnected("inbound-2", PeerDirection.Inbound);
            Assert.Equal(2, scheduler.InboundPeerCount);

            Assert.False(await scheduler.TryReserveSlotAsync(seventh, cts.Token));

            scheduler.ReleaseSlot(sixth, DialOutcome.Failure);
            Assert.True(await scheduler.TryReserveSlotAsync(seventh, cts.Token));
            scheduler.ReleaseSlot(seventh, DialOutcome.Failure);
        }

        [Fact]
        public async Task Given_TrustedCandidate_When_CapAndRatioBoth_Saturated_Then_StillAdmittedButHistoryApplies()
        {
            var clock = new TestClock(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));
            var scheduler = new DialScheduler(
                new DialSchedulerOptions
                {
                    MaxActiveDials = 2,
                    DialHistoryExpiration = TimeSpan.FromMinutes(5),
                    TrustedHistoryExpiration = TimeSpan.FromSeconds(30),
                },
                maxPeers: 2,
                now: clock.Now);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var u1 = Candidate(1);
            var u2 = Candidate(2);
            Assert.True(await scheduler.TryReserveSlotAsync(u1, cts.Token));
            Assert.True(await scheduler.TryReserveSlotAsync(u2, cts.Token));

            var untrusted3 = Candidate(3);
            Assert.False(await scheduler.TryReserveSlotAsync(untrusted3, cts.Token));

            var trusted = Candidate(99, trusted: true);
            Assert.True(await scheduler.TryReserveSlotAsync(trusted, cts.Token));
            scheduler.ReleaseSlot(trusted, DialOutcome.Success);

            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.False(await scheduler.TryReserveSlotAsync(trusted, cts.Token));

            clock.Advance(TimeSpan.FromSeconds(25));
            Assert.True(await scheduler.TryReserveSlotAsync(trusted, cts.Token));
            scheduler.ReleaseSlot(trusted, DialOutcome.Failure);

            scheduler.ReleaseSlot(u1, DialOutcome.Failure);
            scheduler.ReleaseSlot(u2, DialOutcome.Failure);
        }

        [Fact]
        public async Task Given_100ConcurrentCallers_When_AllRaceForSlots_Then_CapNeverExceeded()
        {
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 8,
            }, maxPeers: 1000);

            int observedMax = 0;
            int succeeded = 0;
            var rejected = new ConcurrentBag<int>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
            {
                var c = Candidate(i);
                if (!await scheduler.TryReserveSlotAsync(c, cts.Token))
                {
                    rejected.Add(i);
                    return;
                }
                Interlocked.Increment(ref succeeded);
                int snapshot;
                while (true)
                {
                    snapshot = scheduler.ActiveDialCount;
                    int prev = observedMax;
                    if (snapshot <= prev) break;
                    if (Interlocked.CompareExchange(ref observedMax, snapshot, prev) == prev) break;
                }
                await Task.Delay(5, cts.Token);
                scheduler.ReleaseSlot(c, DialOutcome.Success);
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.True(observedMax <= 8,
                $"Active dial count exceeded cap of 8 (observed peak: {observedMax}).");
            Assert.Equal(100, succeeded + rejected.Count);
            Assert.Equal(0, scheduler.ActiveDialCount);
        }

        [Fact]
        public async Task Given_OutboundPeerDisconnects_When_RatioReChecked_Then_NewOutboundAdmitted()
        {
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 100,
            }, maxPeers: 4);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            for (int i = 0; i < 3; i++)
            {
                var c = Candidate(i);
                Assert.True(await scheduler.TryReserveSlotAsync(c, cts.Token));
                scheduler.ReleaseSlot(c, DialOutcome.Success);
                scheduler.OnPeerConnected(c.Key, PeerDirection.Outbound);
            }
            Assert.Equal(3, scheduler.OutboundPeerCount);

            var fourth = Candidate(3);
            Assert.False(await scheduler.TryReserveSlotAsync(fourth, cts.Token));

            scheduler.OnPeerDisconnected(Candidate(0).Key, PeerDirection.Outbound);
            Assert.Equal(2, scheduler.OutboundPeerCount);
            Assert.True(await scheduler.TryReserveSlotAsync(fourth, cts.Token));
            scheduler.ReleaseSlot(fourth, DialOutcome.Failure);
        }

        [Fact]
        public async Task Given_NullArguments_When_ConstructedOrInvoked_Then_ArgumentExceptionThrown()
        {
            Assert.Throws<ArgumentNullException>(() => new DialScheduler(null, maxPeers: 25));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DialScheduler(new DialSchedulerOptions { MaxActiveDials = 0 }, maxPeers: 25));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DialScheduler(new DialSchedulerOptions(), maxPeers: 0));

            var scheduler = new DialScheduler(new DialSchedulerOptions(), maxPeers: 25);
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => scheduler.TryReserveSlotAsync(null, CancellationToken.None));
            Assert.Throws<ArgumentNullException>(
                () => scheduler.ReleaseSlot(null, DialOutcome.Success));

            Assert.Throws<ArgumentException>(() => new DialCandidate(""));
            Assert.Throws<ArgumentException>(() => new DialCandidate(null));
            Assert.Throws<ArgumentException>(
                () => scheduler.OnPeerConnected("", PeerDirection.Inbound));
            Assert.Throws<ArgumentException>(
                () => scheduler.OnPeerDisconnected(null, PeerDirection.Outbound));
        }

        private sealed class TestClock
        {
            private DateTimeOffset _now;
            public TestClock(DateTimeOffset start) { _now = start; }
            public Func<DateTimeOffset> Now => () => _now;
            public void Advance(TimeSpan delta) { _now = _now.Add(delta); }
        }
    }
}
