using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Trait("Category", "Security")]
    public class S1bR3BondCapSecurityTests
    {
        [Fact]
        public async Task Given_BondTableFilledByAttackerIdentities_When_AnHonestPeerBonds_Then_ItIsAdmittedToTheRoutingTable()
        {
            var key = EthECKey.GenerateKey();
            using var listener = new Discv4Listener(key, new Discv4RoutingTable(key.GetPubKeyNoPrefix()))
            {
                RevalidationInterval = System.TimeSpan.Zero,
                MaxBondedPeers = 3
            };
            listener.Start(0, IPAddress.Loopback);
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            for (var i = 0; i < 3; i++)
            {
                using var attacker = new Discv4RawPeer();
                await attacker.BondAsync(target);
            }

            using var honest = new Discv4RawPeer();
            await honest.BondAsync(target);

            var admitted = listener.Routing.FindClosest(honest.NodeId, 16)
                .Any(n => n.NodeId.SequenceEqual(honest.NodeId));
            Assert.True(admitted, "an honest peer could not bond once free attacker identities filled the bond table");
        }
    }
}
