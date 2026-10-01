using System;
using System.Collections.Concurrent;
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
    public class PeerPoolManagerLivenessFixesTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 120)}{index:x8}@127.0.0.1:{34000 + index}";

        [Fact]
        public async Task Given_TransportFaultMarksTheConnectionDisconnected_When_TheDisconnectedEventFires_Then_ThePoolDisposesTheConnection()
        {
            var enode = MakeEnode(1);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: new[] { enode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);
            var peerRemovedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerRemoved += (_, p) => peerRemovedTcs.TrySetResult(p);

            await pool.StartAsync(CancellationToken.None);
            var peer = await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(IsDisposed(peer.Connection));

            InvokeMarkDisconnected(peer.Connection);

            await peerRemovedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(IsDisposed(peer.Connection),
                "a transport-fault disconnect (MarkDisconnected without Dispose) must still result in the pool disposing the connection so its socket/stream/ping-timer are released");
        }

        [Fact]
        public async Task Given_TargetPeerCountBelowMinKeptActivePeers_When_ActiveExceedsTargetAndPeersGoUnresponsive_Then_TheSweepIsNotDisabled()
        {
            var enode1 = MakeEnode(10);
            var enode2 = MakeEnode(11);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(enode1);
            worker.SetSuccess(enode2);
            var clock = new MutableClock();

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 4,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: new[] { enode1 },
                utcNow: clock.Get);

            var peer1AddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => { if (string.Equals(p.Enode, enode1, StringComparison.OrdinalIgnoreCase)) peer1AddedTcs.TrySetResult(p); };

            await pool.StartAsync(CancellationToken.None);
            await peer1AddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(3, PeerPoolManager.MinKeptActivePeers);

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));

            pool.EnqueueCandidate(enode2);
            await WaitForActiveCountAsync(pool, 2);

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.Equal(1, pool.ActivePeers.Count);
            Assert.False(pool.IsPeerActive(await ResolvePeerIdAsync(pool, enode1)),
                "with TargetPeerCount=1, the sweep floor must be min(MinKeptActivePeers, TargetPeerCount)=1, not the fixed constant 3 — so with 2 active unresponsive peers the sweep must not be disabled");
        }

        [Fact]
        public async Task Given_UselessPeerException_ForATrustedEnode_Then_ItIsNotBanned()
        {
            var trustedEnode = MakeEnode(20);
            var worker = new UselessPeerWorker();
            worker.SetUseless(trustedEnode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: new[] { trustedEnode },
                trustedDialKeys: new[] { trustedEnode });

            await pool.StartAsync(CancellationToken.None);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (worker.HandshakeCount(trustedEnode) < 1 && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            await Task.Delay(200);

            Assert.True(worker.HandshakeCount(trustedEnode) >= 1);
            Assert.False(pool.IsBannedForTest(trustedEnode),
                "a trusted peer's UselessPeerException (transiently behind) must not be banned");
        }

        [Fact]
        public async Task Given_UselessPeerException_ForANonTrustedEnode_Then_ItIsBanned()
        {
            var enode = MakeEnode(21);
            var worker = new UselessPeerWorker();
            worker.SetUseless(enode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: new[] { enode });

            await pool.StartAsync(CancellationToken.None);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!pool.IsBannedForTest(enode) && DateTime.UtcNow < deadline)
                await Task.Delay(25);

            Assert.True(pool.IsBannedForTest(enode),
                "a non-trusted peer's UselessPeerException must still be banned as today");
        }

        [Fact]
        public async Task Given_PoolAtTarget_When_TheTrustedKeeperTicks_Then_ItDialsTheTrustedEnodeDirectly_BypassingThePausedDialLoop()
        {
            var fillerEnode = MakeEnode(30);
            var trustedEnode = MakeEnode(31);
            var worker = new StubHandshakeWorker();
            worker.SetSuccess(fillerEnode);
            worker.SetSuccess(trustedEnode);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 4,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1),
                    TrustedRedialInterval: TimeSpan.FromMilliseconds(50)),
                bootnodes: new[] { fillerEnode },
                trustedDialKeys: new[] { trustedEnode });

            var fillerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => { if (string.Equals(p.Enode, fillerEnode, StringComparison.OrdinalIgnoreCase)) fillerAddedTcs.TrySetResult(p); };

            await pool.StartAsync(CancellationToken.None);
            await fillerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (worker.HandshakeCount(trustedEnode) < 1 && DateTime.UtcNow < deadline)
                await Task.Delay(25);

            Assert.True(pool.ShouldPauseDialingForTest(),
                "the pool must still be at/above target (paused) when the trusted enode gets dialed, proving the keeper bypassed the pause gate");
            Assert.True(worker.HandshakeCount(trustedEnode) >= 1,
                "the trusted peer keeper must dial a not-active trusted enode directly, even while the ordinary dial loop is paused at target");
        }

        private static async Task<Guid> ResolvePeerIdAsync(PeerPoolManager pool, string enode)
        {
            foreach (var peer in pool.ActivePeers)
                if (string.Equals(peer.Enode, enode, StringComparison.OrdinalIgnoreCase))
                    return peer.Id;
            await Task.CompletedTask;
            return Guid.Empty;
        }

        private static async Task WaitForActiveCountAsync(PeerPoolManager pool, int atLeast)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (pool.ActivePeers.Count < atLeast && DateTime.UtcNow < deadline)
                await Task.Delay(25);
            Assert.Equal(atLeast, pool.ActivePeers.Count);
        }

        private static bool IsDisposed(RlpxConnection conn) => conn.IsDisposed;
        private static void InvokeMarkDisconnected(RlpxConnection conn) => conn.MarkDisconnected();

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

        private sealed class UselessPeerWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, byte> _useless = new(StringComparer.OrdinalIgnoreCase);

            public void SetUseless(string enode) => _useless[enode] = 0;
            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var n) ? n : 0;

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                if (_useless.ContainsKey(enode))
                    throw new SyncPeerSession.UselessPeerException(
                        $"Peer Latest=0 < required minimum {minPeerLatestBlock}");
                throw new InvalidOperationException($"no configured outcome for {enode}");
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
