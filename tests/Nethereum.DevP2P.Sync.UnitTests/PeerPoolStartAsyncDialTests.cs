using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.NodeDb;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolStartAsyncDialTests
    {
        private static string MakeEnode(int index) =>
            $"enode://{new string((char)('a' + index), 128)}@127.0.0.{index + 10}:30303";

        [Fact]
        public async Task Given_BootnodesAndCachedPeers_When_StartAsync_Then_EachIsDialledOnce()
        {
            var cachePath = Path.Combine(Path.GetTempPath(), $"peer-cache-{Guid.NewGuid():N}.json");
            try
            {
                var cached = new[] { MakeEnode(1), MakeEnode(2) };
                var boot = new[] { MakeEnode(3), MakeEnode(4) };
                var cache = new PersistentPeerCache(cachePath, _ => { });
                foreach (var enode in cached) cache.RecordSuccess(enode);
                var worker = new CountingHandshakeWorker();

                await using var pool = new PeerPoolManager(
                    worker,
                    new PeerPoolOptions(
                        TargetPeerCount: 8,
                        MaxConcurrentDials: 4,
                        DialBudgetPerSecond: 1000,
                        DialCooldown: TimeSpan.FromMilliseconds(1),
                        MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                    bootnodes: boot,
                    peerCache: cache);

                await pool.StartAsync(CancellationToken.None);
                await WaitUntilAsync(() => pool.ActivePeers.Count == 4);
                await Task.Delay(TimeSpan.FromMilliseconds(400));

                foreach (var enode in cached.Concat(boot))
                    Assert.Equal(1, worker.HandshakeCount(enode));
            }
            finally
            {
                if (File.Exists(cachePath)) File.Delete(cachePath);
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            Assert.True(condition(), "condition not reached within 20s");
        }

        private sealed class CountingHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var n) ? n : 0;

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
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
            public DateTime LastFrameReceivedUtc { get; set; } = DateTime.UtcNow;
            public event EventHandler<IEthPeer>? Disconnected;
        }
    }
}
