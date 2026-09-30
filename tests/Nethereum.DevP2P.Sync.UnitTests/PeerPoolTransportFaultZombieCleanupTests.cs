using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Signer;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolTransportFaultZombieCleanupTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/pool-fault-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 60_000,
            PingIntervalMs = 60_000
        };

        [Fact]
        public async Task Given_ActivePeer_When_TransportDiesWithoutDisconnectFrame_Then_PoolSlotAndSubnetSlotBothRelease()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = BuildConfig();
            var listener = new RlpxListener(serverKey, config);
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            try
            {
                var enodeA = $"enode://{new string('a', 128)}@203.0.113.5:30303";
                var enodeB = $"enode://{new string('b', 128)}@203.0.113.6:30304";

                var worker = new RealLoopbackHandshakeWorker(listener, config, enodeA);

                await using var pool = new PeerPoolManager(
                    worker,
                    new PeerPoolOptions(
                        TargetPeerCount: 5,
                        MaxConcurrentDials: 5,
                        DialBudgetPerSecond: 1000,
                        DialCooldown: TimeSpan.FromMilliseconds(1),
                        MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1),
                        MaxPeersPerIPv4Subnet: 1,
                        IPv4SubnetPrefix: 24),
                    bootnodes: new[] { enodeA });

                var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
                pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);
                var peerRemovedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
                pool.PeerRemoved += (_, p) => peerRemovedTcs.TrySetResult(p);

                await pool.StartAsync(CancellationToken.None);

                var peerA = await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
                var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

                pool.EnqueueCandidate(enodeB);
                await Task.Delay(500);
                Assert.Equal(0, worker.HandshakeCount(enodeB));
                Assert.Single(pool.ActivePeers);

                int disconnectedCount = 0;
                var disconnectedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                peerA.Connection.Disconnected += (_, __) =>
                {
                    Interlocked.Increment(ref disconnectedCount);
                    disconnectedTcs.TrySetResult(true);
                };

                var requestId = peerA.Connection.NextRequestId();
                var pendingRequest = peerA.Connection.SendRequestAsync(
                    sendMsgId: 0x10, payload: Array.Empty<byte>(),
                    expectedResponseMsgId: 0x11, requestId: requestId,
                    timeout: TimeSpan.FromSeconds(30));
                await Task.Delay(500);
                serverConn.Dispose();

                var removedPeer = await peerRemovedTcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(peerA.Id, removedPeer.Id);

                await disconnectedTcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(1, disconnectedCount);
                Assert.Empty(pool.ActivePeers);
                Assert.False(pool.IsPeerActive(peerA.Id));

                pool.EnqueueCandidate(enodeB);
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (worker.HandshakeCount(enodeB) < 1 && DateTime.UtcNow < deadline)
                    await Task.Delay(100);
                Assert.True(worker.HandshakeCount(enodeB) >= 1,
                    "expected enodeB's dial to be attempted once peer A's subnet slot released");

                try { await pendingRequest; } catch { }
            }
            finally
            {
                try { await listener.StopAsync(); } catch { }
            }
        }

        private sealed class RealLoopbackHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly RlpxListener _listener;
            private readonly DevP2PConfig _config;
            private readonly string _realEnode;
            private readonly ConcurrentDictionary<string, int> _counts =
                new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public RealLoopbackHandshakeWorker(RlpxListener listener, DevP2PConfig config, string realEnode)
            {
                _listener = listener;
                _config = config;
                _realEnode = realEnode;
            }

            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var n) ? n : 0;

            public async Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);

                if (!string.Equals(enode, _realEnode, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"no real peer configured for {enode}");

                var clientKey = EthECKey.GenerateKey();
                var conn = new RlpxConnection(clientKey, _config);
                await conn.ConnectAsync("127.0.0.1", _listener.Port, _listener.NodeId, ct).ConfigureAwait(false);
                return new LoopbackConnEthPeer(enode, conn);
            }
        }

        private sealed class LoopbackConnEthPeer : IEthPeer
        {
            public LoopbackConnEthPeer(string enode, RlpxConnection conn)
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
