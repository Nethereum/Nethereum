using System;
using Nethereum.DevP2P.Sync;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapStateCooldownTests
    {
        [Fact]
        public void PeerFarBehindHead_GetsBriefBench()
        {
            var cd = FetchRequestScheduler.ChooseSnapStateCooldown(peerLatestBlock: 100, networkHeadBlock: 1000, isTrusted: false);
            Assert.Equal(6, cd.TotalSeconds);
        }

        [Fact]
        public void PeerAtHead_ButEmpty_GetsFullQuarantine()
        {
            var cd = FetchRequestScheduler.ChooseSnapStateCooldown(peerLatestBlock: 1000, networkHeadBlock: 1000, isTrusted: false);
            Assert.Equal(120, cd.TotalSeconds);
        }

        [Fact]
        public void TrustedPeerAtHead_GetsShortTrustedCooldown()
        {
            var cd = FetchRequestScheduler.ChooseSnapStateCooldown(peerLatestBlock: 1000, networkHeadBlock: 1000, isTrusted: true);
            Assert.Equal(20, cd.TotalSeconds);
        }

        [Theory]
        [InlineData(997, false)]
        [InlineData(996, false)]
        [InlineData(995, true)]
        [InlineData(0, true)]
        public void MarginBoundary_DecidesBehind(ulong peerLatest, bool expectBehind)
        {
            var cd = FetchRequestScheduler.ChooseSnapStateCooldown(peerLatest, networkHeadBlock: 1000, isTrusted: false);
            Assert.Equal(expectBehind ? 6 : 120, cd.TotalSeconds);
        }

        [Fact]
        public void TargetRootChanged_ReAdmitsQuarantinedPeers()
        {
            var peer = new BenchFakePeer();
            var scheduler = new FetchRequestScheduler(
                new BenchFakePool(peer), new PeerRequestWorker(), new FetchRequestSchedulerOptions());

            scheduler.QuarantineSnapState(peer);
            Assert.True(scheduler.IsSnapStateQuarantined(peer.Id), "bench precondition");

            scheduler.OnTargetRootChanged();

            Assert.False(scheduler.IsSnapStateQuarantined(peer.Id));
        }

        private sealed class BenchFakePool : IPeerPool
        {
            private readonly System.Collections.Generic.List<IEthPeer> _peers;
            public BenchFakePool(params IEthPeer[] peers)
                => _peers = new System.Collections.Generic.List<IEthPeer>(peers);
            public System.Collections.Generic.IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }
            public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
            public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task BanAndDropAsync(string enode, string reason, System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task DropAsync(Guid peerId, string reason, System.Threading.CancellationToken ct)
                => System.Threading.Tasks.Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public System.Threading.Tasks.Task ClearAllBansAsync() => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.ValueTask DisposeAsync() => default;
        }

        private sealed class BenchFakePeer : IEthPeer
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode => "enode://bench@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 1_000UL;
            public uint PeerForkHash => 0;
            public Nethereum.DevP2P.Rlpx.RlpxConnection Connection => null;
            public event EventHandler<IEthPeer> Disconnected { add { } remove { } }
        }
    }
}
