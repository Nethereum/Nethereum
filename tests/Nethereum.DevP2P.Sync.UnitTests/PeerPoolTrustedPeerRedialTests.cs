using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerPoolTrustedPeerRedialTests
    {
        private static readonly string LocalGeth = $"enode://{new string('a', 128)}@127.0.0.1:30307";
        private static readonly string RemotePeer = $"enode://{new string('b', 128)}@112.154.155.200:20001";

        private static PeerPoolOptions KeeperTickingFast() =>
            new PeerPoolOptions(
                TargetPeerCount: 1,
                MaxConcurrentDials: 4,
                DialBudgetPerSecond: 1000,
                DialCooldown: TimeSpan.FromMilliseconds(1),
                MinDialIntervalPerHost: TimeSpan.FromMilliseconds(1),
                TrustedRedialInterval: TimeSpan.FromMilliseconds(20));

        [Fact]
        public async Task Given_TwoTrustedPeersAndOneIsDroppedWhileThePoolIsAtTarget_When_TheKeeperTicks_Then_TheDroppedPeerIsRedialledAndReAdded()
        {
            var worker = new SucceedingHandshakeWorker(LocalGeth, RemotePeer);
            var added = new ConcurrentQueue<IEthPeer>();

            await using var pool = new PeerPoolManager(
                worker, KeeperTickingFast(), trustedDialKeys: new[] { LocalGeth, RemotePeer });
            pool.PeerAdded += (_, p) => added.Enqueue(p);

            await pool.StartAsync(CancellationToken.None);
            await WaitUntilAsync(() => ActiveEnodes(pool).Length == 2);
            var localSession = pool.ActivePeers.Single(p => p.Enode == LocalGeth);

            await pool.DropAsync(localSession.Id, "unresponsive", CancellationToken.None);

            await WaitUntilAsync(() => added.Count(p => p.Enode == LocalGeth) >= 2);
            Assert.Equal(2, worker.HandshakeCount(LocalGeth));
            Assert.False(pool.IsPeerActive(localSession.Id));
            Assert.Contains(LocalGeth, ActiveEnodes(pool));
            Assert.Contains(RemotePeer, ActiveEnodes(pool));
        }

        [Fact]
        public async Task Given_ATrustedDialKeyThatIsTwoEnodesJoinedByAComma_When_TheKeeperTicks_Then_ItIsNeverHandedToTheHandshakeAndIsLoggedOnceAsMalformed()
        {
            var joined = $"{LocalGeth},{RemotePeer}";
            var worker = new SucceedingHandshakeWorker(LocalGeth, RemotePeer);
            var logger = new RecordingLogger();

            await using var pool = new PeerPoolManager(
                worker, KeeperTickingFast(), logger: logger, trustedDialKeys: new[] { joined });

            await pool.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            Assert.Equal(0, worker.TotalHandshakes);
            Assert.Empty(pool.ActivePeers);
            Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(joined));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("dialing directly"));
        }

        private static string[] ActiveEnodes(PeerPoolManager pool) =>
            pool.ActivePeers.Select(p => p.Enode).ToArray();

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            Assert.True(condition(), "condition not reached within 5s");
        }

        private sealed class SucceedingHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
            private readonly string[] _reachable;

            public SucceedingHandshakeWorker(params string[] reachable) => _reachable = reachable;

            public int HandshakeCount(string enode) => _counts.TryGetValue(enode, out var n) ? n : 0;

            public int TotalHandshakes => _counts.Values.Sum();

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                if (!_reachable.Contains(enode, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"unreachable {enode}");
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

        private sealed class RecordingLogger : ILogger<PeerPoolManager>
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => Entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
