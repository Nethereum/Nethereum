using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolSubnetDiversityTests
    {
        private static string MakeEnode(string ip, int port, int nodeIndex) =>
            $"enode://{new string('a', 120)}{nodeIndex:x8}@{ip}:{port}";

        [Fact]
        public async Task DialLoop_RejectsCandidates_OnceSubnetQuotaFilled()
        {
            var enodes = Enumerable.Range(1, 8)
                .Select(i => MakeEnode($"203.0.113.{i}", 30303, i))
                .ToArray();

            var worker = new SuccessfulHandshakeWorker();
            foreach (var e in enodes) worker.SetSuccess(e);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 16,
                    MaxConcurrentDials: 8,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1),
                    MaxPeersPerIPv4Subnet: 3,
                    IPv4SubnetPrefix: 24),
                bootnodes: enodes);

            await pool.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            Assert.Equal(3, worker.TotalHandshakes);
            Assert.Equal(3, pool.ActivePeers.Count);
        }

        private sealed class SuccessfulHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, byte> _success = new(StringComparer.OrdinalIgnoreCase);
            private int _total;

            public int TotalHandshakes => Volatile.Read(ref _total);

            public void SetSuccess(string enode) => _success[enode] = 0;

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                Interlocked.Increment(ref _total);
                if (!_success.ContainsKey(enode))
                    throw new InvalidOperationException($"No configured outcome for {enode}");
                IEthPeer peer = new StubPeer(enode);
                return Task.FromResult(peer);
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
