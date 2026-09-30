using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolResponsiveActiveGraceTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 120)}{index:x8}@127.0.0.1:{32000 + index}";

        private static PeerPoolManager BuildPool(
            StubHandshakeWorker worker,
            int targetPeerCount,
            MutableClock clock,
            string[] bootnodes,
            string[]? trustedDialKeys = null)
            => new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: targetPeerCount,
                    MaxConcurrentDials: Math.Max(4, targetPeerCount + 8),
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: bootnodes,
                trustedDialKeys: trustedDialKeys,
                utcNow: clock.Get);

        private static IEthPeer FindPeer(PeerPoolManager pool, string enode)
        {
            foreach (var peer in pool.ActivePeers)
                if (string.Equals(peer.Enode, enode, StringComparison.OrdinalIgnoreCase))
                    return peer;
            throw new InvalidOperationException($"peer not found for {enode}");
        }

        [Fact]
        public async Task Given_one_peer_reports_success_and_one_never_does_When_grace_elapses_Then_only_the_responsive_one_counts()
        {
            var enodeA = MakeEnode(1);
            var enodeB = MakeEnode(2);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enodeA);
            worker.SetSuccess(enodeB);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 2, clock, bootnodes: new[] { enodeA, enodeB });

            var addedCount = 0;
            var bothAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, __) => { if (Interlocked.Increment(ref addedCount) == 2) bothAddedTcs.TrySetResult(true); };

            await pool.StartAsync(CancellationToken.None);
            await bothAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(2, pool.GetResponsiveActiveCountForTest());

            var peerA = FindPeer(pool, enodeA);

            clock.Advance(PeerPoolManager.ResponsiveGrace - TimeSpan.FromSeconds(10));
            pool.ReportSuccess(peerA.Id);
            clock.Advance(TimeSpan.FromSeconds(20));

            Assert.Equal(1, pool.GetResponsiveActiveCountForTest());
        }

        [Fact]
        public async Task Given_a_freshly_connected_peer_with_no_success_yet_When_the_sweep_runs_Then_it_is_not_dropped_and_still_counts()
        {
            var enode = MakeEnode(3);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enode);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 1, clock, bootnodes: new[] { enode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);
            var peerRemoved = false;
            pool.PeerRemoved += (_, __) => peerRemoved = true;

            await pool.StartAsync(CancellationToken.None);
            var peer = await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(1, pool.GetResponsiveActiveCountForTest());

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.False(peerRemoved, "a fresh peer within NewPeerGrace must never be dropped by the sweep");
            Assert.True(pool.IsPeerActive(peer.Id));
            Assert.Equal(1, pool.GetResponsiveActiveCountForTest());
        }

        [Fact]
        public async Task Given_a_trusted_peer_with_no_success_and_active_peers_above_the_floor_When_grace_elapses_and_the_sweep_runs_Then_it_is_not_dropped()
        {
            var trustedEnode = MakeEnode(4);
            var others = new[] { MakeEnode(5), MakeEnode(6), MakeEnode(7) };
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(trustedEnode, isTrusted: true);
            foreach (var e in others) worker.SetSuccess(e);
            var clock = new MutableClock();

            var allEnodes = new[] { trustedEnode }.Concat(others).ToArray();
            await using var pool = BuildPool(
                worker, targetPeerCount: 4, clock, bootnodes: allEnodes, trustedDialKeys: new[] { trustedEnode });

            var addedCount = 0;
            var allAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, __) => { if (Interlocked.Increment(ref addedCount) == 4) allAddedTcs.TrySetResult(true); };
            var removedEnodes = new ConcurrentBag<string>();
            pool.PeerRemoved += (_, p) => removedEnodes.Add(p.Enode);

            await pool.StartAsync(CancellationToken.None);
            await allAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, worker.HandshakeCount(trustedEnode));

            clock.Advance(PeerPoolManager.ResponsiveGrace - TimeSpan.FromSeconds(10));
            foreach (var e in others)
                pool.ReportSuccess(FindPeer(pool, e).Id);
            clock.Advance(TimeSpan.FromSeconds(20));

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.DoesNotContain(trustedEnode, removedEnodes);
            Assert.Contains(pool.ActivePeers, p => string.Equals(p.Enode, trustedEnode, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(4, pool.ActivePeers.Count);
        }

        [Fact]
        public async Task Given_a_non_trusted_peer_with_no_success_and_active_peers_above_the_floor_When_grace_elapses_and_the_sweep_runs_Then_it_is_dropped_and_not_banned()
        {
            var enodes = new[] { MakeEnode(9), MakeEnode(10), MakeEnode(11), MakeEnode(12) };
            var worker = new StubHandshakeWorker();
            foreach (var e in enodes) worker.SetSuccess(e);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 4, clock, bootnodes: enodes);

            var addedCount = 0;
            var allAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, __) => { if (Interlocked.Increment(ref addedCount) == 4) allAddedTcs.TrySetResult(true); };
            var removedEnodes = new ConcurrentBag<string>();
            pool.PeerRemoved += (_, p) => removedEnodes.Add(p.Enode);

            await pool.StartAsync(CancellationToken.None);
            await allAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            clock.Advance(PeerPoolManager.ResponsiveGrace - TimeSpan.FromSeconds(10));
            foreach (var e in enodes.Take(3))
                pool.ReportSuccess(FindPeer(pool, e).Id);
            clock.Advance(TimeSpan.FromSeconds(20));

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.Contains(enodes[3], removedEnodes);
            Assert.Equal(3, pool.ActivePeers.Count);
            Assert.False(pool.IsBannedForTest(enodes[3]),
                "the grace sweep must call DropAsync, never BanAndDropAsync — a non-trusted peer must not be suppressed");
        }

        [Fact]
        public async Task Given_active_peers_at_or_below_the_floor_When_an_unresponsive_non_trusted_peer_is_swept_Then_it_is_not_dropped()
        {
            var enode = MakeEnode(13);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enode);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 1, clock, bootnodes: new[] { enode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);
            var peerRemoved = false;
            pool.PeerRemoved += (_, __) => peerRemoved = true;

            await pool.StartAsync(CancellationToken.None);
            await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.False(peerRemoved,
                "the sweep must not drop a peer when active peers are at/below the floor, even if it looks unresponsive");
            Assert.Equal(1, pool.ActivePeers.Count);
        }

        [Fact]
        public async Task Given_a_freshly_connected_peer_with_no_success_yet_When_NewPeerGrace_elapses_but_not_ResponsiveGrace_Then_it_still_counts_as_responsive()
        {
            var enode = MakeEnode(14);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enode);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 1, clock, bootnodes: new[] { enode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);
            await pool.StartAsync(CancellationToken.None);
            await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            clock.Advance(PeerPoolManager.NewPeerGrace + TimeSpan.FromSeconds(1));

            Assert.Equal(1, pool.GetResponsiveActiveCountForTest());
        }

        [Fact]
        public async Task Given_more_than_target_plus_four_candidates_queued_in_one_batch_When_the_gate_opens_Then_active_never_exceeds_target_plus_four()
        {
            const int target = 2;
            var cap = target + PeerPoolManager.HardCapAboveTarget;
            var worker = new StubHandshakeWorker();
            var enodes = new List<string>();
            for (int i = 0; i < cap + 5; i++)
            {
                var enode = MakeEnode(40 + i);
                enodes.Add(enode);
                worker.SetSuccess(enode);
            }

            var clock = new MutableClock();
            await using var pool = BuildPool(worker, targetPeerCount: target, clock, bootnodes: enodes.ToArray());

            await pool.StartAsync(CancellationToken.None);

            await Task.Delay(1000);

            Assert.True(pool.ActivePeers.Count <= cap,
                $"a single burst of {enodes.Count} candidates (>{cap}) must not push active peers past the hard cap; got {pool.ActivePeers.Count}");
        }

        [Fact]
        public async Task Given_several_in_grace_zombies_When_the_dial_loop_admits_replacements_Then_active_never_exceeds_target_plus_four()
        {
            const int target = 2;
            var worker = new StubHandshakeWorker();
            var enodes = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                var enode = MakeEnode(10 + i);
                enodes.Add(enode);
                worker.SetSuccess(enode);
            }

            var clock = new MutableClock();
            await using var pool = BuildPool(worker, targetPeerCount: target, clock, bootnodes: new[] { enodes[0], enodes[1] });

            await pool.StartAsync(CancellationToken.None);

            await WaitForActiveCountAsync(pool, 2);

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));
            pool.EnqueueCandidate(enodes[2]);
            pool.EnqueueCandidate(enodes[3]);
            await WaitForActiveCountAsync(pool, 4);

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));
            pool.EnqueueCandidate(enodes[4]);
            pool.EnqueueCandidate(enodes[5]);
            await WaitForActiveCountAsync(pool, target + PeerPoolManager.HardCapAboveTarget);

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));
            pool.EnqueueCandidate(enodes[6]);
            pool.EnqueueCandidate(enodes[7]);
            await Task.Delay(750);

            Assert.Equal(0, worker.HandshakeCount(enodes[6]));
            Assert.Equal(0, worker.HandshakeCount(enodes[7]));
            Assert.Equal(target + PeerPoolManager.HardCapAboveTarget, pool.ActivePeers.Count);
        }

        [Fact]
        public async Task Given_a_peer_nearing_grace_expiry_When_ReportSuccess_is_called_again_Then_it_becomes_responsive_again()
        {
            var enode = MakeEnode(20);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enode);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 1, clock, bootnodes: new[] { enode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);
            await pool.StartAsync(CancellationToken.None);
            var peer = await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            pool.ReportSuccess(peer.Id);
            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));

            Assert.Equal(0, pool.GetResponsiveActiveCountForTest());

            pool.ReportSuccess(peer.Id);

            Assert.Equal(1, pool.GetResponsiveActiveCountForTest());
        }

        [Fact]
        public async Task Given_a_peer_with_a_recent_frame_timestamp_and_no_fetch_success_and_active_peers_above_the_floor_When_grace_elapses_and_the_sweep_runs_Then_it_is_responsive_and_not_dropped()
        {
            var enodes = new[] { MakeEnode(30), MakeEnode(31), MakeEnode(32), MakeEnode(33) };
            var worker = new StubHandshakeWorker();
            foreach (var e in enodes) worker.SetSuccess(e);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 4, clock, bootnodes: enodes);

            var addedCount = 0;
            var allAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, __) => { if (Interlocked.Increment(ref addedCount) == 4) allAddedTcs.TrySetResult(true); };
            var removedEnodes = new ConcurrentBag<string>();
            pool.PeerRemoved += (_, p) => removedEnodes.Add(p.Enode);

            await pool.StartAsync(CancellationToken.None);
            await allAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));
            foreach (var e in enodes.Take(3))
                pool.ReportSuccess(FindPeer(pool, e).Id);

            var quietPeer = (ConnectedStubPeer)FindPeer(pool, enodes[3]);
            quietPeer.LastFrameReceivedUtc = clock.Get();

            Assert.Equal(4, pool.GetResponsiveActiveCountForTest());

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.DoesNotContain(enodes[3], removedEnodes);
            Assert.Equal(4, pool.ActivePeers.Count);
        }

        [Fact]
        public async Task Given_a_peer_with_a_stale_frame_timestamp_and_no_fetch_success_and_active_peers_above_the_floor_When_grace_elapses_and_the_sweep_runs_Then_it_is_dropped()
        {
            var enodes = new[] { MakeEnode(34), MakeEnode(35), MakeEnode(36), MakeEnode(37) };
            var worker = new StubHandshakeWorker();
            foreach (var e in enodes) worker.SetSuccess(e);
            var clock = new MutableClock();

            await using var pool = BuildPool(worker, targetPeerCount: 4, clock, bootnodes: enodes);

            var addedCount = 0;
            var allAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, __) => { if (Interlocked.Increment(ref addedCount) == 4) allAddedTcs.TrySetResult(true); };
            var removedEnodes = new ConcurrentBag<string>();
            pool.PeerRemoved += (_, p) => removedEnodes.Add(p.Enode);

            await pool.StartAsync(CancellationToken.None);
            await allAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));
            foreach (var e in enodes.Take(3))
                pool.ReportSuccess(FindPeer(pool, e).Id);

            var quietPeer = (ConnectedStubPeer)FindPeer(pool, enodes[3]);
            quietPeer.LastFrameReceivedUtc = clock.Get() - PeerPoolManager.TransportLivenessWindow - TimeSpan.FromSeconds(1);

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.Contains(enodes[3], removedEnodes);
            Assert.Equal(3, pool.ActivePeers.Count);
        }

        private static async Task WaitForActiveCountAsync(PeerPoolManager pool, int atLeast)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (pool.ActivePeers.Count < atLeast && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Assert.Equal(atLeast, pool.ActivePeers.Count);
        }

        private sealed class MutableClock
        {
            private long _ticks = DateTime.UtcNow.Ticks;
            public DateTime Get() => new DateTime(Interlocked.Read(ref _ticks), DateTimeKind.Utc);
            public void Advance(TimeSpan by) => Interlocked.Exchange(ref _ticks, Get().Add(by).Ticks);
        }

        private sealed class StubHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, bool> _success = new(StringComparer.OrdinalIgnoreCase);

            public void SetSuccess(string enode, bool isTrusted = false) => _success[enode] = isTrusted;
            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var n) ? n : 0;

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                if (!_success.TryGetValue(enode, out var isTrusted))
                    throw new InvalidOperationException($"no configured outcome for {enode}");
                return Task.FromResult((IEthPeer)new ConnectedStubPeer(enode, isTrusted));
            }
        }

        private sealed class ConnectedStubPeer : IEthPeer
        {
            public ConnectedStubPeer(string enode, bool isTrusted = false)
            {
                Enode = enode;
                Host = enode;
                IsTrusted = isTrusted;
                Connection = new RlpxConnection(EthECKey.GenerateKey());
                Connection.Disconnected += (_, __) => Disconnected?.Invoke(this, this);
            }

            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public bool IsTrusted { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection { get; }
            public DateTime LastFrameReceivedUtc { get; set; } = DateTime.UtcNow;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}
