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
    public class PeerPoolNodeIdInFlightDedupTests
    {
        private static readonly string SharedPubkey = new string('c', 120) + "deadbeef";

        [Fact]
        public async Task Given_SameNodeId_OfferedUnderTwoDifferentEnodeStrings_When_DialedTogether_Then_OnlyOneInFlightDialStarts()
        {
            var formA = $"enode://{SharedPubkey}@127.0.0.1:34000";
            var formB = $"enode://{SharedPubkey}@localhost:34000";

            var worker = new CountingHandshakeWorker();
            worker.SetSuccess(formA);
            worker.SetSuccess(formB);

            await using var pool = new PeerPoolManager(
                worker,
                new PeerPoolOptions(
                    TargetPeerCount: 2,
                    MaxConcurrentDials: 2,
                    DialBudgetPerSecond: 1000,
                    DialCooldown: TimeSpan.FromMilliseconds(1),
                    MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1)),
                bootnodes: new[] { formA, formB });

            await pool.StartAsync(CancellationToken.None);

            await Task.Delay(TimeSpan.FromSeconds(1));

            Assert.Equal(1, worker.TotalHandshakes);
            Assert.Single(pool.ActivePeers);
        }

        private sealed class CountingHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, byte> _success = new(StringComparer.OrdinalIgnoreCase);
            private int _total;

            public int TotalHandshakes => Volatile.Read(ref _total);
            public void SetSuccess(string enode) => _success[enode] = 0;

            public async Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                Interlocked.Increment(ref _total);
                await Task.Delay(TimeSpan.FromMilliseconds(300), ct).ConfigureAwait(false);
                if (!_success.ContainsKey(enode))
                    throw new InvalidOperationException($"no configured outcome for {enode}");
                return new StubPeer(enode);
            }
        }

        private sealed class StubPeer : IEthPeer
        {
            public StubPeer(string enode)
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
