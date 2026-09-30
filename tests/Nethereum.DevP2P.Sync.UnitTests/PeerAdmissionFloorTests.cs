using Nethereum.DevP2P.Sync.Peering;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class PeerAdmissionFloorTests
    {
        private const ulong Floor = 14_000_000;

        [Fact]
        public void Given_AnEth68Peer_When_ItReportsLatest0_Then_ItIsAdmitted_NotRejectedAsBehind()
        {
            Assert.False(SyncPeerSession.ShouldRejectAsBehind(ethVersion: 68, peerLatestBlock: 0, minPeerLatestBlock: Floor));
        }

        [Fact]
        public void Given_AnEth69Peer_When_ItReportsAHeadBelowTheFloor_Then_ItIsRejectedAsBehind()
        {
            Assert.True(SyncPeerSession.ShouldRejectAsBehind(69, peerLatestBlock: 0, minPeerLatestBlock: Floor));
            Assert.True(SyncPeerSession.ShouldRejectAsBehind(69, peerLatestBlock: 13_000_000, minPeerLatestBlock: Floor));
        }

        [Fact]
        public void Given_AnEth69Peer_When_ItReportsAHeadAtOrAboveTheFloor_Then_ItIsAdmitted()
        {
            Assert.False(SyncPeerSession.ShouldRejectAsBehind(69, peerLatestBlock: Floor, minPeerLatestBlock: Floor));
            Assert.False(SyncPeerSession.ShouldRejectAsBehind(69, peerLatestBlock: 25_900_000, minPeerLatestBlock: Floor));
        }

        [Fact]
        public void Given_AnEth70OrEth71Peer_When_ItReportsLatest0_Then_ItIsRejected_TheyCarryARealHead()
        {
            Assert.True(SyncPeerSession.ShouldRejectAsBehind(70, peerLatestBlock: 0, minPeerLatestBlock: Floor));
            Assert.True(SyncPeerSession.ShouldRejectAsBehind(71, peerLatestBlock: 0, minPeerLatestBlock: Floor));
        }

        [Fact]
        public void Given_TheFloorIsDisabled_When_ZeroIsPassed_Then_NobodyIsRejected_AnyVersion()
        {
            Assert.False(SyncPeerSession.ShouldRejectAsBehind(68, peerLatestBlock: 0, minPeerLatestBlock: 0));
            Assert.False(SyncPeerSession.ShouldRejectAsBehind(69, peerLatestBlock: 0, minPeerLatestBlock: 0));
        }
    }
}
