using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class Discv4PendingPingSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PendingPingTableFilledByManyIdentities_When_HonestPeerBonds_Then_BondStillSucceeds()
        {
            var key = EthECKey.GenerateKey();
            using var listener = new Discv4Listener(key, new Discv4RoutingTable(key.GetPubKeyNoPrefix()))
            {
                LocalEnrEncoded = new byte[] { 0xc2, 0x01, 0x02 }
            };
            listener.Start(0, IPAddress.Loopback);
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            var flood = listener.MaxPendingPings + 200;

            using (var attacker = new Discv4RawPeer())
            {
                for (var i = 0; i < flood; i++)
                    await attacker.SendPingAsync(target, EthECKey.GenerateKey());
            }
            await Task.Delay(1500);

            using var honest = new Discv4RawPeer();
            var bonded = await honest.BondAsync(target);

            var data = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(BitConverter.GetBytes(Discv4RawPeer.Future()).Reverse().SkipWhile(b => b == 0).ToArray()));
            await honest.SendAsync(Discv4RawPeer.BuildPacket(honest.Key, Discv4MessageType.EnrRequest, data), target);
            var received = await honest.CollectAsync(TimeSpan.FromSeconds(1));

            Assert.True(bonded && received.Any(r => r.Packet.Type == Discv4MessageType.EnrResponse),
                $"after {flood} spoofed-identity PINGs the pending-ping table (cap {listener.MaxPendingPings}, TTL 30 s) refuses the back-PING to an honest peer, so it can never bond (geth tracks a pending PING for respTimeout 500 ms)");
        }
    }
}
