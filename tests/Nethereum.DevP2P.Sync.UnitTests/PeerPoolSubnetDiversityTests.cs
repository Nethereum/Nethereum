using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    /// <summary>
    /// Integration coverage for the outbound subnet diversity gate in
    /// <see cref="PeerPoolManager"/>. Eclipse defence — the dial loop must
    /// reject candidates from a /24 IPv4 subnet once that subnet's quota is
    /// full, even when each individual host passes the per-IP gate.
    /// </summary>
    public class PeerPoolSubnetDiversityTests
    {
        // Non-loopback / non-private documentation block (RFC 5737) so the
        // SubnetTracker actually counts it.
        private static string MakeEnode(string ip, int port) =>
            $"enode://{new string('a', 128)}@{ip}:{port}";

        [Fact]
        public async Task DialLoop_RejectsCandidates_OnceSubnetQuotaFilled()
        {
            // Eight enodes share the same /24 (203.0.113.0/24). Subnet cap
            // = 3, so only the first three should ever reach the handshake
            // worker — the remaining five must be rejected on subnet
            // admission before any dial is attempted.
            var enodes = Enumerable.Range(1, 8)
                .Select(i => MakeEnode($"203.0.113.{i}", 30303))
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
