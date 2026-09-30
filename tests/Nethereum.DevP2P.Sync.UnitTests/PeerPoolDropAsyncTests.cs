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
    public class PeerPoolDropAsyncTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 128)}@127.0.0.1:{30000 + index}";

        [Fact]
        public async Task DropAsync_DisposesTheConnection_RemovesThePeer_AndDoesNotBanTheEnode()
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

            await pool.DropAsync(peer.Id, "repeated eth timeout", CancellationToken.None);

            var removed = await peerRemovedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(peer.Id, removed.Id);
            Assert.False(pool.IsPeerActive(peer.Id));
            Assert.Empty(pool.ActivePeers);

            pool.EnqueueCandidate(enode);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (worker.HandshakeCount(enode) < 2 && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Assert.True(worker.HandshakeCount(enode) >= 2,
                "DropAsync must not ban the enode: an explicit re-dial right after drop must not be suppressed");
        }

        [Fact]
        public async Task DropAsync_TrustedPeer_IsRedialedByTheDisconnectHandler_WithoutBeingBanned()
        {
            var enode = MakeEnode(2);
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
                bootnodes: new[] { enode },
                trustedDialKeys: new[] { enode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);

            await pool.StartAsync(CancellationToken.None);
            var peer = await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, worker.HandshakeCount(enode));

            await pool.DropAsync(peer.Id, "repeated eth timeout", CancellationToken.None);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (worker.HandshakeCount(enode) < 2 && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Assert.True(worker.HandshakeCount(enode) >= 2,
                "a dropped trusted peer must be redialed by the disconnect handler without ever being banned");
        }

        [Fact]
        public async Task DropAsync_UnknownPeerId_IsANoOp()
        {
            var worker = new StubHandshakeWorker();
            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1)),
                bootnodes: Array.Empty<string>());

            await pool.StartAsync(CancellationToken.None);

            await pool.DropAsync(Guid.NewGuid(), "no such peer", CancellationToken.None);

            Assert.Empty(pool.ActivePeers);
        }

        private sealed class StubHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
            private readonly ConcurrentDictionary<string, bool> _success = new(StringComparer.OrdinalIgnoreCase);

            public void SetSuccess(string enode) => _success[enode] = true;
            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var n) ? n : 0;

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                if (!_success.ContainsKey(enode))
                    throw new InvalidOperationException($"no configured outcome for {enode}");
                return Task.FromResult((IEthPeer)new ConnectedStubPeer(enode));
            }
        }

        private sealed class ConnectedStubPeer : IEthPeer
        {
            public ConnectedStubPeer(string enode)
            {
                Enode = enode;
                Host = enode;
                Connection = new RlpxConnection(EthECKey.GenerateKey());
                Connection.Disconnected += (_, __) => Disconnected?.Invoke(this, this);
            }

            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 22_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection { get; }
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}
