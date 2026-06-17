using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolManagerDialSchedulerTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task DialRate_StaysWithinBudget()
        {
            // 20 candidates, budget = 5/s, MaxConcurrentDials high enough not
            // to be the bottleneck. After ~1s of dialing the worker should
            // have seen between 5 and 12 attempts — we allow a slack window
            // for the initial burst of (budget) free tokens plus partial
            // accrual during scheduler wake-ups.
            var enodes = Enumerable.Range(1, 20).Select(MakeEnode).ToArray();
            var worker = new SlowHandshakeWorker(TimeSpan.FromMilliseconds(50));
            foreach (var e in enodes) worker.SetFailure(e);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 30,
                    MaxConcurrentDials: 30,
                    DialBudgetPerSecond: 5,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: enodes);

            var sw = Stopwatch.StartNew();
            await pool.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));
            var afterOneSecond = worker.TotalHandshakes;

            // Initial burst = full bucket (5) + ~5 accrued over the second.
            // Allow 5..15 as a robust window — well below 20 which would
            // indicate the bucket is not gating at all.
            Assert.InRange(afterOneSecond, 5, 15);
        }

        [Fact]
        public async Task RecentlyFailed_NotRedialedWithinInterval()
        {
            // Single enode, MinDialIntervalPerHost = 800ms. After the first
            // failure the dial loop must not re-dial it for >= 800ms.
            var enode = MakeEnode(1);
            var worker = new SlowHandshakeWorker(TimeSpan.FromMilliseconds(10));
            worker.SetFailure(enode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(800)),
                bootnodes: new[] { enode });

            await pool.StartAsync(CancellationToken.None);

            // Give the first dial time to fail and the dial loop time to spin
            // back around — without a cooldown gate it would dial again within
            // a few ms.
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            var afterFirstFail = worker.HandshakeCount(enode);
            Assert.Equal(1, afterFirstFail);

            // Re-enqueue while still inside the cooldown window — must be a no-op.
            pool.EnqueueCandidate(enode);
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Assert.Equal(1, worker.HandshakeCount(enode));

            // After the cooldown window the next enqueue should be honored.
            await Task.Delay(TimeSpan.FromMilliseconds(700));
            pool.EnqueueCandidate(enode);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.True(worker.HandshakeCount(enode) >= 2,
                $"Expected re-dial after cooldown elapsed, got {worker.HandshakeCount(enode)} attempts.");
        }

        [Fact]
        public async Task HighScorePeer_DialedFirst_WhenBothInBatch()
        {
            // Pre-populate the persistent cache so one enode has a clearly
            // higher score than the other. Then enqueue both candidates back-
            // to-back so the dial-loop drain catches them in the same batch.
            var cachePath = Path.Combine(Path.GetTempPath(), $"peer-cache-{Guid.NewGuid():N}.json");
            try
            {
                var cache = new PersistentPeerCache(cachePath, _ => { });
                var highScore = MakeEnode(1);
                var lowScore = MakeEnode(2);

                // 5 successful, 0 failed → high score. lowScore stays cache-absent
                // (PeerScore.Unknown, ComputedScore = 0) — strictly below highScore.
                for (int i = 0; i < 5; i++) cache.RecordSuccess(highScore);

                var worker = new OrderRecordingHandshakeWorker();
                worker.SetSuccess(highScore);
                worker.SetSuccess(lowScore);
                // Gate the first dial so both candidates queue up before the
                // loop drains and ranks the batch.
                worker.PauseFirstHandshake = true;

                await using var pool = new PeerPoolManager(
                    worker,
                    new PeerPoolOptions(
                        TargetPeerCount: 2,
                        MaxConcurrentDials: 1,
                        DialBudgetPerSecond: 1000,
                        DialCooldown: TimeSpan.FromMilliseconds(1)),
                    bootnodes: Array.Empty<string>(),
                    peerCache: cache);

                await pool.StartAsync(CancellationToken.None);
                // Enqueue lowScore FIRST so insertion order would put it ahead
                // — score ranking should flip the order.
                pool.EnqueueCandidate(lowScore);
                pool.EnqueueCandidate(highScore);

                await Task.Delay(TimeSpan.FromMilliseconds(200));
                worker.ReleaseFirstHandshake();
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                Assert.Equal(2, worker.DialOrder.Count);
                Assert.Equal(highScore, worker.DialOrder[0]);
                Assert.Equal(lowScore, worker.DialOrder[1]);
            }
            finally
            {
                if (File.Exists(cachePath)) File.Delete(cachePath);
            }
        }

        private sealed class SlowHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly TimeSpan _delay;
            private readonly ConcurrentDictionary<string, OutcomeKind> _outcomes = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
            private int _total;

            public SlowHandshakeWorker(TimeSpan delay) { _delay = delay; }

            public void SetFailure(string enode) => _outcomes[enode] = OutcomeKind.Failure;
            public void SetSuccess(string enode) => _outcomes[enode] = OutcomeKind.Success;

            public int TotalHandshakes => Volatile.Read(ref _total);
            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var c) ? c : 0;

            public async Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                Interlocked.Increment(ref _total);
                await Task.Delay(_delay, ct).ConfigureAwait(false);
                if (!_outcomes.TryGetValue(enode, out var outcome))
                    throw new InvalidOperationException($"No configured outcome for {enode}");
                if (outcome == OutcomeKind.Failure)
                    throw new InvalidOperationException($"stub-failure {enode}");
                return new StubPeer(enode);
            }

            private enum OutcomeKind { Success, Failure }
        }

        private sealed class OrderRecordingHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, bool> _success = new(StringComparer.OrdinalIgnoreCase);
            private readonly TaskCompletionSource<bool> _firstReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _firstCounted;

            public bool PauseFirstHandshake { get; set; }
            public List<string> DialOrder { get; } = new();
            public void SetSuccess(string enode) => _success[enode] = true;
            public void ReleaseFirstHandshake() => _firstReleased.TrySetResult(true);

            public async Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                // Record the order in which dials start (NOT complete) — that's
                // what proves the loop ranked them before dispatch.
                lock (DialOrder) DialOrder.Add(enode);

                if (PauseFirstHandshake && Interlocked.Increment(ref _firstCounted) == 1)
                {
                    using var reg = ct.Register(() => _firstReleased.TrySetCanceled(ct));
                    await _firstReleased.Task.ConfigureAwait(false);
                }

                if (!_success.ContainsKey(enode))
                    throw new InvalidOperationException($"No configured outcome for {enode}");
                return new StubPeer(enode);
            }
        }

        private sealed class StubPeer : IEthPeer
        {
            public StubPeer(string enode) { Enode = enode; Host = enode; }
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}
