using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv5
{
    public class Discv5PendingOutboundConcurrencyTests
    {
        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "BUG-D5SM-2 dual-index consistency under concurrent same-peer dials")]
        public void Given_ManyConcurrentDialsToTheSamePeer_When_Added_Then_NonceIndexHasNoOrphans()
        {
            var mgr = new Discv5SessionManager(EthECKey.GenerateKey());

            var nodeId = new byte[32];
            for (int i = 0; i < nodeId.Length; i++) nodeId[i] = (byte)(0x11 + i);
            var addr = new IPEndPoint(IPAddress.Parse("203.0.113.9"), 30303);
            var peerPub = new byte[33];
            peerPub[0] = 0x02;

            Parallel.For(0, 512, _ =>
                mgr.BuildInitialOrdinaryPacket(nodeId, addr, new byte[] { 0x01 }, peerPub));

            Assert.Equal(1, mgr.PendingOutboundCount);
            Assert.Equal(mgr.PendingOutboundCount, mgr.PendingOutboundNonceIndexCount);
        }
    }
}
