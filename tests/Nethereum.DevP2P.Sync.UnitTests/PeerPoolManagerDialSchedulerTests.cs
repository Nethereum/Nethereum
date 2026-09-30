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
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolManagerDialSchedulerTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 120)}{index:x8}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task DialRate_StaysWithinBudget()
        {
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

            Assert.InRange(afterOneSecond, 5, 15);
        }

        [Fact]
        public async Task RecentlyFailed_NotRedialedWithinInterval()
        {
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

            await Task.Delay(TimeSpan.FromMilliseconds(300));
            var afterFirstFail = worker.HandshakeCount(enode);
            Assert.Equal(1, afterFirstFail);

            pool.EnqueueCandidate(enode);
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Assert.Equal(1, worker.HandshakeCount(enode));

            await Task.Delay(TimeSpan.FromMilliseconds(700));
            pool.EnqueueCandidate(enode);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.True(worker.HandshakeCount(enode) >= 2,
                $"Expected re-dial after cooldown elapsed, got {worker.HandshakeCount(enode)} attempts.");
        }

        [Fact]
        public async Task TrustedPeer_RedialedByKeeper_AfterFailedDial()
        {
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
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1),
                    TrustedRedialInterval: TimeSpan.FromMilliseconds(100)),
                bootnodes: new[] { enode },
                trustedDialKeys: new[] { enode });

            await pool.StartAsync(CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(600));

            Assert.True(worker.HandshakeCount(enode) >= 3,
                $"Trusted peer must be redialed by the keeper after failed dials; got {worker.HandshakeCount(enode)} attempts.");
        }

        [Fact]
        public async Task HighScorePeer_DialedFirst_WhenBothInBatch()
        {
            var cachePath = Path.Combine(Path.GetTempPath(), $"peer-cache-{Guid.NewGuid():N}.json");
            try
            {
                var cache = new PersistentPeerCache(cachePath, _ => { });
                var highScore = MakeEnode(1);
                var lowScore = MakeEnode(2);

                for (int i = 0; i < 5; i++) cache.RecordSuccess(highScore);

                var worker = new OrderRecordingHandshakeWorker();
                worker.SetSuccess(highScore);
                worker.SetSuccess(lowScore);
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

        [Fact]
        public async Task Given_PeerBannedForTransientError_When_ThirtySecondsElapse_Then_PeerIsDialableAgain()
        {
            var enode = MakeEnode(1);
            var worker = new SlowHandshakeWorker(TimeSpan.FromMilliseconds(10));
            worker.SetSuccess(enode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: Array.Empty<string>());

            await pool.StartAsync(CancellationToken.None);

            await pool.BanAndDropAsync(enode, "test-ban", CancellationToken.None);

            pool.EnqueueCandidate(enode);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            var handshakesWhileBanned = worker.HandshakeCount(enode);
            Assert.Equal(0, handshakesWhileBanned);

            await Task.Delay(TimeSpan.FromSeconds(31));

            pool.EnqueueCandidate(enode);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            var handshakesAfterBanExpires = worker.HandshakeCount(enode);

            Assert.True(handshakesAfterBanExpires > 0,
                $"Expected peer to be dialed after ban expires, got {handshakesAfterBanExpires} handshakes.");
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
