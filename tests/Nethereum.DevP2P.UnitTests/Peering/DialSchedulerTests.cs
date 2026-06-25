using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Peering;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Peering
{
    /// <summary>
    /// Spec-level tests for <see cref="DialScheduler"/>. Mirrors geth's
    /// <c>p2p/dial_test.go</c>: concurrent-dial cap honoured under contention,
    /// recent-dial history suppresses retries until expiration, the
    /// inbound/outbound ratio cap rejects excess outbound dials, trusted
    /// candidates bypass the cap and ratio but still receive history
    /// suppression, and the whole thing is safe under 100 concurrent
    /// callers.
    /// </summary>
    public class DialSchedulerTests
    {
        private static DialCandidate Candidate(int index, bool trusted = false) =>
            new DialCandidate($"enode://peer-{index}", trusted);

        [Fact]
        public async Task Given_ConcurrentCap_When_OneExtraReserves_Then_BlocksUntilRelease()
        {
            // Cap = 16, MaxPeers high so ratio is not the limiting factor.
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 16,
                MaxPeers = 1000,
            });

            // Acquire 16 slots — all should complete immediately.
            var firstWave = new DialCandidate[16];
            for (int i = 0; i < 16; i++) firstWave[i] = Candidate(i);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            foreach (var c in firstWave)
                Assert.True(await scheduler.TryReserveSlotAsync(c, cts.Token));

            Assert.Equal(16, scheduler.ActiveDialCount);

            // 17th reserve must NOT complete while all 16 slots are held.
            var seventeenth = Candidate(16);
            var pending = scheduler.TryReserveSlotAsync(seventeenth, cts.Token);

            // Give the await a chance to enter the wait state, then confirm it
            // hasn't satisfied. Use a short delay — we're proving "blocks",
            // not measuring wall-clock — so 100ms is plenty above any
            // scheduler-quanta noise without slowing the suite.
            await Task.Delay(100);
            Assert.False(pending.IsCompleted,
                "17th TryReserveSlotAsync completed while all 16 slots were held.");

            // Release one — the pending 17th must now resolve true.
            scheduler.ReleaseSlot(firstWave[0], DialOutcome.Success);
            var seventeenthResult = await pending;
            Assert.True(seventeenthResult);
            Assert.Equal(16, scheduler.ActiveDialCount);

            // Drain. All other 15 slots + the just-acquired 17th.
            for (int i = 1; i < 16; i++)
                scheduler.ReleaseSlot(firstWave[i], DialOutcome.Failure);
            scheduler.ReleaseSlot(seventeenth, DialOutcome.Failure);
            Assert.Equal(0, scheduler.ActiveDialCount);
        }

        [Fact]
        public async Task Given_RecentDialHistory_When_SameCandidateRetried_Then_SuppressedUntilExpiration()
        {
            // Inject a controllable clock so we can step past the 5-minute
            // history window without sleeping the test thread.
            var clock = new TestClock(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));
            var scheduler = new DialScheduler(
                new DialSchedulerOptions
                {
                    MaxActiveDials = 4,
                    DialHistoryExpiration = TimeSpan.FromMinutes(5),
                    MaxPeers = 1000,
                },
                clock.Now);

            var peer = Candidate(1);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // First reserve + release succeeds.
            Assert.True(await scheduler.TryReserveSlotAsync(peer, cts.Token));
            scheduler.ReleaseSlot(peer, DialOutcome.Failure);

            // Inside the history window — must be suppressed.
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.False(await scheduler.TryReserveSlotAsync(peer, cts.Token));

            clock.Advance(TimeSpan.FromMinutes(3));
            Assert.False(await scheduler.TryReserveSlotAsync(peer, cts.Token));

            // Past the window — admission resumes.
            clock.Advance(TimeSpan.FromMinutes(2));
            Assert.True(await scheduler.TryReserveSlotAsync(peer, cts.Token));
            scheduler.ReleaseSlot(peer, DialOutcome.Success);
        }

        [Fact]
        public async Task Given_RatioCap_When_OutboundCapHit_Then_RejectedButInboundStillCounted()
        {
            // MaxPeers = 10 → OutboundCap = 10/2 + 1 = 6. Cap counts in-flight
            // dials + live outbound peers.
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 100,
                MaxPeers = 10,
            });
            Assert.Equal(6, scheduler.OutboundCap);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Reserve + release + mark connected outbound for 5 peers. After
            // ReleaseSlot the in-flight count drops; OnPeerConnected
            // (outbound) re-bumps the cap-relevant counter via the live
            // outbound count. Net outbound charge = 5.
            for (int i = 0; i < 5; i++)
            {
                var c = Candidate(i);
                Assert.True(await scheduler.TryReserveSlotAsync(c, cts.Token));
                scheduler.ReleaseSlot(c, DialOutcome.Success);
                scheduler.OnPeerConnected(c.Key, PeerDirection.Outbound);
            }
            Assert.Equal(5, scheduler.OutboundPeerCount);

            // Reserve a 6th outbound dial — that brings the total to 5 live +
            // 1 in-flight = 6 = cap. Allowed by ratio (strictly less than
            // cap was the gate at the point of admission). Hold the slot
            // (do not release yet) so the 7th sees both 5 live + 1 in-flight.
            var sixth = Candidate(5);
            Assert.True(await scheduler.TryReserveSlotAsync(sixth, cts.Token));

            // 7th outbound TryReserveSlotAsync must be rejected by the ratio
            // gate (5 live + 1 in-flight = 6 ≥ cap).
            var seventh = Candidate(6);
            Assert.False(await scheduler.TryReserveSlotAsync(seventh, cts.Token));

            // Inbound peers do not consume outbound slots — the inbound
            // counter grows independently, the ratio check still passes
            // for outbound candidates with the same total outbound load.
            scheduler.OnPeerConnected("inbound-1", PeerDirection.Inbound);
            scheduler.OnPeerConnected("inbound-2", PeerDirection.Inbound);
            Assert.Equal(2, scheduler.InboundPeerCount);

            // The ratio gate STILL rejects the 7th outbound candidate even
            // though we've added inbound peers — inbound does not free
            // outbound capacity.
            Assert.False(await scheduler.TryReserveSlotAsync(seventh, cts.Token));

            // Release the 6th in-flight dial without it becoming live —
            // outbound load drops to 5, ratio admits the 7th.
            scheduler.ReleaseSlot(sixth, DialOutcome.Failure);
            Assert.True(await scheduler.TryReserveSlotAsync(seventh, cts.Token));
            scheduler.ReleaseSlot(seventh, DialOutcome.Failure);
        }

        [Fact]
        public async Task Given_TrustedCandidate_When_CapAndRatioBoth_Saturated_Then_StillAdmittedButHistoryApplies()
        {
            // MaxActiveDials = 2 (low cap), MaxPeers = 2 → OutboundCap = 2/2 + 1 = 2.
            // Saturate both gates with untrusted candidates, then prove a
            // trusted candidate bypasses them.
            var clock = new TestClock(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));
            var scheduler = new DialScheduler(
                new DialSchedulerOptions
                {
                    MaxActiveDials = 2,
                    MaxPeers = 2,
                    DialHistoryExpiration = TimeSpan.FromMinutes(5),
                    TrustedHistoryExpiration = TimeSpan.FromSeconds(30),
                },
                clock.Now);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Saturate the concurrent cap with 2 untrusted dials.
            var u1 = Candidate(1);
            var u2 = Candidate(2);
            Assert.True(await scheduler.TryReserveSlotAsync(u1, cts.Token));
            Assert.True(await scheduler.TryReserveSlotAsync(u2, cts.Token));

            // An untrusted 3rd reserve fails the ratio gate (2 in-flight =
            // cap of 2) before the wait on the concurrent cap is even
            // attempted — ratio gate is the conservative pre-check.
            var untrusted3 = Candidate(3);
            Assert.False(await scheduler.TryReserveSlotAsync(untrusted3, cts.Token));

            // Trusted candidate ignores cap AND ratio — admitted immediately.
            var trusted = Candidate(99, trusted: true);
            Assert.True(await scheduler.TryReserveSlotAsync(trusted, cts.Token));
            scheduler.ReleaseSlot(trusted, DialOutcome.Success);

            // Re-reserving the same trusted candidate inside the trusted
            // history window (30s) must be suppressed.
            clock.Advance(TimeSpan.FromSeconds(10));
            Assert.False(await scheduler.TryReserveSlotAsync(trusted, cts.Token));

            // Past the trusted window but before the untrusted window —
            // trusted admitted, while an untrusted with the same key
            // would still be suppressed if it had been recorded as
            // untrusted. (Trusted classification is per-candidate; we
            // record on whichever flavor last touched.)
            clock.Advance(TimeSpan.FromSeconds(25));
            Assert.True(await scheduler.TryReserveSlotAsync(trusted, cts.Token));
            scheduler.ReleaseSlot(trusted, DialOutcome.Failure);

            // Drain the untrusted dials so the scheduler is left in a clean
            // state at the end of the test.
            scheduler.ReleaseSlot(u1, DialOutcome.Failure);
            scheduler.ReleaseSlot(u2, DialOutcome.Failure);
        }

        [Fact]
        public async Task Given_100ConcurrentCallers_When_AllRaceForSlots_Then_CapNeverExceeded()
        {
            // Stress test: 100 callers compete for 8 concurrent slots. Cap
            // must never be exceeded at any point and no exception escapes.
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 8,
                MaxPeers = 1000, // ratio not the gate
            });

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
                // Brief simulated dial work, then release.
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
            // Disconnect path must decrement the outbound counter so the
            // ratio gate opens back up. Otherwise a fully-cycled connection
            // would permanently consume a slot.
            var scheduler = new DialScheduler(new DialSchedulerOptions
            {
                MaxActiveDials = 100,
                MaxPeers = 4, // cap = 4/2 + 1 = 3
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Connect 3 outbound peers — that fully saturates the ratio.
            for (int i = 0; i < 3; i++)
            {
                var c = Candidate(i);
                Assert.True(await scheduler.TryReserveSlotAsync(c, cts.Token));
                scheduler.ReleaseSlot(c, DialOutcome.Success);
                scheduler.OnPeerConnected(c.Key, PeerDirection.Outbound);
            }
            Assert.Equal(3, scheduler.OutboundPeerCount);

            // 4th outbound dial rejected by ratio.
            var fourth = Candidate(3);
            Assert.False(await scheduler.TryReserveSlotAsync(fourth, cts.Token));

            // Disconnect one — ratio reopens, 4th admitted.
            scheduler.OnPeerDisconnected(Candidate(0).Key, PeerDirection.Outbound);
            Assert.Equal(2, scheduler.OutboundPeerCount);
            Assert.True(await scheduler.TryReserveSlotAsync(fourth, cts.Token));
            scheduler.ReleaseSlot(fourth, DialOutcome.Failure);
        }

        [Fact]
        public async Task Given_NullArguments_When_ConstructedOrInvoked_Then_ArgumentExceptionThrown()
        {
            Assert.Throws<ArgumentNullException>(() => new DialScheduler(null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DialScheduler(new DialSchedulerOptions { MaxActiveDials = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DialScheduler(new DialSchedulerOptions { MaxPeers = 0 }));

            var scheduler = new DialScheduler(new DialSchedulerOptions());
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
