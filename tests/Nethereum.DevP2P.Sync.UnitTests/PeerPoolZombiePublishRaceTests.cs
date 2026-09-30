using System;
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
    public class PeerPoolZombiePublishRaceTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string('a', 120)}{index:x8}@127.0.0.1:{33000 + index}";

        [Fact]
        public async Task Given_PeerConnectionAlreadyDisconnected_When_Published_Then_ItIsCleanedUpAndNotLeftAsAZombie()
        {
            var enode = MakeEnode(1);
            var worker = new AlreadyDisconnectedHandshakeWorker();

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    MaxConcurrentDials: 1,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: new[] { enode });

            var removedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerRemoved += (_, p) => removedTcs.TrySetResult(p);

            await pool.StartAsync(CancellationToken.None);

            var removed = await removedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(pool.IsPeerActive(removed.Id));
            Assert.Empty(pool.ActivePeers);

            pool.EnqueueCandidate(enode);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (worker.HandshakeCount < 2 && DateTime.UtcNow < deadline)
                await Task.Delay(50);

            Assert.True(worker.HandshakeCount >= 2,
                "the node must remain dialable after a publish-time disconnect race, not permanently zombied under its node-id key");
        }

        private sealed class AlreadyDisconnectedHandshakeWorker : IPeerHandshakeWorker
        {
            private int _count;
            public int HandshakeCount => Volatile.Read(ref _count);

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                Interlocked.Increment(ref _count);
                var conn = new RlpxConnection(EthECKey.GenerateKey());
                var peer = new StubPeer(enode, conn);
                conn.Dispose();
                return Task.FromResult((IEthPeer)peer);
            }
        }

        private sealed class StubPeer : IEthPeer
        {
            public StubPeer(string enode, RlpxConnection conn)
            {
                Enode = enode;
                Host = enode;
                Connection = conn;
                conn.Disconnected += (_, __) => Disconnected?.Invoke(this, this);
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
