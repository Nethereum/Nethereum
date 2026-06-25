using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class PeerPoolE2ETests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task PoolReachesTarget_FromBootnodeSeed()
        {
            var enodes = Enumerable.Range(1, 3).Select(MakeEnode).ToArray();
            var worker = new ControlledHandshakeWorker();
            foreach (var enode in enodes) worker.SetSuccess(enode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(TargetPeerCount: 3, MaxConcurrentDials: 3),
                bootnodes: enodes);

            await pool.StartAsync(CancellationToken.None);

            await WaitForCountAsync(pool, 3, TimeSpan.FromSeconds(2));

            Assert.Equal(3, pool.ActivePeers.Count);
            foreach (var enode in enodes)
            {
                Assert.Equal(1, worker.HandshakeCount(enode));
            }
            Assert.Equal(3, worker.TotalHandshakes);
        }

        [Fact]
        public async Task PeerDisconnected_AutomaticallyReplaced()
        {
            var enodes = Enumerable.Range(1, 5).Select(MakeEnode).ToArray();
            var worker = new ControlledHandshakeWorker();
            foreach (var enode in enodes) worker.SetSuccess(enode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 3,
                    MaxConcurrentDials: 3,
                    DialCooldown: TimeSpan.FromMilliseconds(1)),
                bootnodes: enodes);

            await pool.StartAsync(CancellationToken.None);
            await WaitForCountAsync(pool, 3, TimeSpan.FromSeconds(2));

            var victim = pool.ActivePeers.First();
            worker.SimulateDisconnect(victim.Enode);

            await WaitForReplacementAsync(pool, victim.Id, TimeSpan.FromSeconds(2));

            Assert.Equal(3, pool.ActivePeers.Count);
            Assert.DoesNotContain(pool.ActivePeers, p => p.Id == victim.Id);
            Assert.Equal(1, worker.HandshakeCount(victim.Enode));
            Assert.Equal(4, worker.TotalHandshakes);
        }

        [Fact]
        public async Task UselessPeerRejected_NotInPool()
        {
            var healthy = Enumerable.Range(1, 2).Select(i => MakeEnode(i)).ToArray();
            var useless = Enumerable.Range(10, 3).Select(i => MakeEnode(i)).ToArray();
            var worker = new ControlledHandshakeWorker();
            foreach (var e in healthy) worker.SetSuccess(e);
            foreach (var e in useless) worker.SetUseless(e);

            var allBootnodes = useless.Concat(healthy).ToArray();

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 2,
                    MaxConcurrentDials: 5,
                    MinPeerLatestBlock: 1_000_000),
                bootnodes: allBootnodes);

            await pool.StartAsync(CancellationToken.None);
            await WaitForCountAsync(pool, 2, TimeSpan.FromSeconds(2));

            Assert.Equal(2, pool.ActivePeers.Count);
            foreach (var e in healthy)
                Assert.Contains(pool.ActivePeers, p => string.Equals(p.Enode, e, StringComparison.OrdinalIgnoreCase));
            foreach (var e in useless)
                Assert.DoesNotContain(pool.ActivePeers, p => string.Equals(p.Enode, e, StringComparison.OrdinalIgnoreCase));
            foreach (var e in useless)
                Assert.Equal(1, worker.HandshakeCount(e));
        }

        [Fact]
        public async Task BannedPeerNotRedialed()
        {
            var enodes = Enumerable.Range(1, 2).Select(MakeEnode).ToArray();
            var worker = new ControlledHandshakeWorker();
            foreach (var e in enodes) worker.SetSuccess(e);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 2,
                    MaxConcurrentDials: 2,
                    DialCooldown: TimeSpan.FromMilliseconds(1)),
                bootnodes: enodes);

            await pool.StartAsync(CancellationToken.None);
            await WaitForCountAsync(pool, 2, TimeSpan.FromSeconds(2));
            Assert.Equal(2, worker.TotalHandshakes);

            await pool.BanAndDropAsync(enodes[0], "test ban", CancellationToken.None);
            worker.SimulateDisconnect(enodes[0]);

            await Task.Delay(300);
            Assert.Equal(1, pool.ActivePeers.Count);
            Assert.Equal(2, worker.TotalHandshakes);

            await pool.ClearAllBansAsync();
            pool.EnqueueCandidate(enodes[0]);
            await WaitForCountAsync(pool, 2, TimeSpan.FromSeconds(2));
            Assert.Equal(3, worker.TotalHandshakes);
        }

        [Fact]
        public async Task SuccessfulHandshake_RecordsScoreInCache()
        {
            var cachePath = Path.Combine(Path.GetTempPath(), $"peer-cache-{Guid.NewGuid():N}.json");
            try
            {
                var cache = new PersistentPeerCache(cachePath, _ => { });
                var enode = MakeEnode(1);
                var worker = new ControlledHandshakeWorker();
                worker.SetSuccess(enode);

                await using var pool = new PeerPoolManager(
                    worker,
                    new PeerPoolOptions(TargetPeerCount: 1, MaxConcurrentDials: 1),
                    bootnodes: new[] { enode },
                    peerCache: cache);

                await pool.StartAsync(CancellationToken.None);
                await WaitForCountAsync(pool, 1, TimeSpan.FromSeconds(2));

                var score = pool.GetScore(enode);
                Assert.False(score.IsUnknown);
                Assert.Equal(1, score.SuccessCount);
                Assert.Equal(0, score.FailureCount);
                Assert.True(score.ComputedScore > 0);
                Assert.True(score.LastSeenUtc > DateTimeOffset.UtcNow.AddMinutes(-1));

                Assert.Equal(PeerScore.Unknown, pool.GetScore(MakeEnode(99)));
            }
            finally
            {
                if (File.Exists(cachePath)) File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task FailedHandshake_RecordsFailureInCache_AfterPriorSuccess()
        {
            var cachePath = Path.Combine(Path.GetTempPath(), $"peer-cache-{Guid.NewGuid():N}.json");
            try
            {
                var cache = new PersistentPeerCache(cachePath, _ => { });
                var enode = MakeEnode(1);
                cache.RecordSuccess(enode);

                var worker = new ControlledHandshakeWorker();
                worker.SetUseless(enode);

                await using var pool = new PeerPoolManager(
                    worker,
                    new PeerPoolOptions(TargetPeerCount: 1, MaxConcurrentDials: 1, MinPeerLatestBlock: 1_000_000),
                    bootnodes: new[] { enode },
                    peerCache: cache);

                await pool.StartAsync(CancellationToken.None);
                await Task.Delay(300);

                Assert.Empty(pool.ActivePeers);
                var score = pool.GetScore(enode);
                Assert.Equal(1, score.SuccessCount);
                Assert.Equal(1, score.FailureCount);
                Assert.True(score.ComputedScore > 0);
            }
            finally
            {
                if (File.Exists(cachePath)) File.Delete(cachePath);
            }
        }

        [Fact]
        public async Task DialCascade_FallsThroughOnEmpty()
        {
            var late = MakeEnode(101);
            var worker = new ControlledHandshakeWorker();
            worker.SetSuccess(late);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(TargetPeerCount: 1, MaxConcurrentDials: 1),
                bootnodes: Array.Empty<string>());

            await pool.StartAsync(CancellationToken.None);
            await Task.Delay(150);
            Assert.Empty(pool.ActivePeers);
            Assert.Equal(0, worker.TotalHandshakes);

            Assert.True(pool.EnqueueCandidate(late));

            await WaitForCountAsync(pool, 1, TimeSpan.FromSeconds(2));
            Assert.Equal(1, pool.ActivePeers.Count);
            Assert.Equal(late, pool.ActivePeers.First().Enode);
            Assert.Equal(1, worker.TotalHandshakes);
        }

        private static async Task WaitForCountAsync(IPeerPool pool, int target, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (pool.ActivePeers.Count >= target) return;
                await Task.Delay(25);
            }
            throw new TimeoutException(
                $"Pool did not reach {target} peers within {timeout.TotalSeconds:F1}s (got {pool.ActivePeers.Count}).");
        }

        private static async Task WaitForReplacementAsync(IPeerPool pool, Guid victimId, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (pool.ActivePeers.Count >= pool.TargetPeerCount
                    && !pool.ActivePeers.Any(p => p.Id == victimId))
                    return;
                await Task.Delay(25);
            }
            throw new TimeoutException(
                $"Pool did not replace victim peer within {timeout.TotalSeconds:F1}s.");
        }

        private sealed class ControlledHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, OutcomeKind> _outcomes = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, StubPeer> _activePeers = new(StringComparer.OrdinalIgnoreCase);
            private int _total;

            public void SetSuccess(string enode) => _outcomes[enode] = OutcomeKind.Success;
            public void SetUseless(string enode) => _outcomes[enode] = OutcomeKind.Useless;

            public int HandshakeCount(string enode) =>
                _counts.TryGetValue(enode, out var c) ? c : 0;

            public int TotalHandshakes => Volatile.Read(ref _total);

            public void SimulateDisconnect(string enode)
            {
                if (_activePeers.TryRemove(enode, out var peer))
                {
                    peer.TriggerDisconnect();
                }
            }

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                Interlocked.Increment(ref _total);

                if (!_outcomes.TryGetValue(enode, out var outcome))
                    throw new InvalidOperationException($"No configured outcome for {enode}");

                if (outcome == OutcomeKind.Useless)
                    throw new MainnetPeerSession.UselessPeerException($"stub-useless {enode}");

                var peer = new StubPeer(enode);
                _activePeers[enode] = peer;
                return Task.FromResult<IEthPeer>(peer);
            }

            private enum OutcomeKind { Success, Useless }
        }

        private sealed class StubPeer : IEthPeer
        {
            public StubPeer(string enode)
            {
                Enode = enode;
                Host = enode;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection? Connection => null;
            RlpxConnection IEthPeer.Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
            public void TriggerDisconnect() => Disconnected?.Invoke(this, this);
        }
    }
}
