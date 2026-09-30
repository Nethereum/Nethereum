using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Chain.TestData.Vectors;
using Xunit;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class PeerPoolTrustedNodeIdentityRealLoopbackTests
    {
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                await Task.Delay(50);
            }
            return condition();
        }

        private static string ToLocalhostAliasForm(string enode)
            => enode.Replace("127.0.0.1", "localhost", StringComparison.OrdinalIgnoreCase);

        [Fact]
        public async Task TrustedPeer_ConnectedViaRealHandshake_IsTrustedBeforePeerAddedFires()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 3);
            await new WorkloadV1().BuildAsync(sequencer);

            await using var trustedServer = await WireServerNode.StartAsync(sequencer);
            await using var other1 = await WireServerNode.StartAsync(sequencer);
            await using var other2 = await WireServerNode.StartAsync(sequencer);
            await using var other3 = await WireServerNode.StartAsync(sequencer);

            var clock = new MutableClock();
            await using var pool = new PeerPoolManager(
                new WorkloadHandshakeWorker(trustedServer.GenesisHash, trustedServer.NetworkId),
                new PeerPoolOptions(TargetPeerCount: 4, MinPeerLatestBlock: 0),
                trustedDialKeys: new[] { trustedServer.Enode },
                utcNow: clock.Get);

            bool? isTrustedInsideHandler = null;
            var trustedAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var removedEnodes = new ConcurrentBag<string>();
            pool.PeerAdded += (_, p) =>
            {
                if (string.Equals(p.Enode, trustedServer.Enode, StringComparison.OrdinalIgnoreCase))
                {
                    isTrustedInsideHandler = p.IsTrusted;
                    trustedAddedTcs.TrySetResult(true);
                }
            };
            pool.PeerRemoved += (_, p) => removedEnodes.Add(p.Enode);

            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(trustedServer.Enode);
            pool.EnqueueCandidate(other1.Enode);
            pool.EnqueueCandidate(other2.Enode);
            pool.EnqueueCandidate(other3.Enode);

            await trustedAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(isTrustedInsideHandler,
                "the trusted peer must already report IsTrusted==true inside the PeerAdded handler — trust must be materialized before publish");

            var allConnected = await WaitUntilAsync(() => pool.ActivePeers.Count == 4, TimeSpan.FromSeconds(20));
            Assert.True(allConnected, "follower did not establish all four peers over loopback RLPx");

            var trustedPeer = pool.ActivePeers.First(
                p => string.Equals(p.Enode, trustedServer.Enode, StringComparison.OrdinalIgnoreCase));
            Assert.True(trustedPeer.IsTrusted);

            clock.Advance(PeerPoolManager.ResponsiveGrace + TimeSpan.FromSeconds(1));
            foreach (var otherEnode in new[] { other1.Enode, other2.Enode, other3.Enode })
            {
                var p = pool.ActivePeers.First(x => string.Equals(x.Enode, otherEnode, StringComparison.OrdinalIgnoreCase));
                pool.ReportSuccess(p.Id);
            }

            await pool.SweepUnresponsivePeersForTestAsync(CancellationToken.None);

            Assert.DoesNotContain(trustedServer.Enode, removedEnodes);
            Assert.Contains(pool.ActivePeers,
                p => string.Equals(p.Enode, trustedServer.Enode, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task TrustedPeer_DialedUnderAlternateEnodeString_IsStillTrusted()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 3);
            await new WorkloadV1().BuildAsync(sequencer);

            await using var trustedServer = await WireServerNode.StartAsync(sequencer);

            var canonicalEnode = trustedServer.Enode;
            var altEnode = ToLocalhostAliasForm(canonicalEnode);
            Assert.NotEqual(canonicalEnode, altEnode, StringComparer.OrdinalIgnoreCase);

            var handshakeCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var countingWorker = new CountingHandshakeWorker(
                new WorkloadHandshakeWorker(trustedServer.GenesisHash, trustedServer.NetworkId),
                handshakeCounts);

            await using var pool = new PeerPoolManager(
                countingWorker,
                new PeerPoolOptions(
                    TargetPeerCount: 1,
                    TrustedRedialInterval: TimeSpan.FromMilliseconds(200)),
                trustedDialKeys: new[] { canonicalEnode });

            var peerAddedTcs = new TaskCompletionSource<IEthPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.PeerAdded += (_, p) => peerAddedTcs.TrySetResult(p);

            await pool.StartAsync(CancellationToken.None);
            pool.EnqueueCandidate(altEnode);

            var peer = await peerAddedTcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(peer.IsTrusted,
                "a trusted node dialed under a different (but node-id-equivalent) enode string must still be marked trusted");

            await Task.Delay(TimeSpan.FromSeconds(1));

            var totalHandshakes = handshakeCounts.Values.Sum();
            Assert.Equal(1, totalHandshakes);
        }

        private sealed class MutableClock
        {
            private long _ticks = DateTime.UtcNow.Ticks;
            public DateTime Get() => new DateTime(Interlocked.Read(ref _ticks), DateTimeKind.Utc);
            public void Advance(TimeSpan by) => Interlocked.Exchange(ref _ticks, Get().Add(by).Ticks);
        }

        private sealed class CountingHandshakeWorker : IPeerHandshakeWorker
        {
            private readonly IPeerHandshakeWorker _inner;
            private readonly ConcurrentDictionary<string, int> _counts;

            public CountingHandshakeWorker(IPeerHandshakeWorker inner, ConcurrentDictionary<string, int> counts)
            {
                _inner = inner;
                _counts = counts;
            }

            public Task<IEthPeer> HandshakeAsync(
                string enode, TimeSpan timeout, ulong minPeerLatestBlock, CancellationToken ct)
            {
                _counts.AddOrUpdate(enode, 1, (_, prev) => prev + 1);
                return _inner.HandshakeAsync(enode, timeout, minPeerLatestBlock, ct);
            }
        }
    }
}
