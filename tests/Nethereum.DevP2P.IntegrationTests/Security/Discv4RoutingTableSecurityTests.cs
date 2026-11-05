using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv4RoutingTableSecurityTests
    {
        private static Discv4Listener StartListener()
        {
            var key = EthECKey.GenerateKey();
            var listener = new Discv4Listener(key, new Discv4RoutingTable(key.GetPubKeyNoPrefix()));
            listener.Start(0, IPAddress.Loopback);
            return listener;
        }

        private static bool Contains(Discv4Listener listener, byte[] nodeId)
            => listener.Routing.AllNodes().Any(n => ByteUtil.AreEqual(n.NodeId, nodeId));

        private static async Task<bool> AppearsWithin(Discv4Listener listener, byte[] nodeId, TimeSpan window)
        {
            var deadline = DateTime.UtcNow + window;
            while (DateTime.UtcNow < deadline)
            {
                if (Contains(listener, nodeId)) return true;
                await Task.Delay(25);
            }
            return false;
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_UnsolicitedPongFromUnknownKey_When_ListenerReceivesIt_Then_SenderIsNotAddedToRoutingTable()
        {
            using var listener = StartListener();
            using var attacker = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            await attacker.SendPongAsync(target, new byte[32]);

            Assert.False(await AppearsWithin(listener, attacker.NodeId, TimeSpan.FromSeconds(1)),
                "a PONG that answers no PING of ours put its sender in the routing table (geth v4_udp.go:722 drops it with errUnsolicitedReply)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PingFromKeyThatNeverAnswersTheBackPing_When_ListenerRespondsToIt_Then_SenderIsNotInRoutingTable()
        {
            using var listener = StartListener();
            using var attacker = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            await attacker.SendPingAsync(target);

            Assert.False(await AppearsWithin(listener, attacker.NodeId, TimeSpan.FromSeconds(1)),
                "an unbonded PING sender was inserted before any endpoint proof (geth v4_udp.go:699-704 inserts only after the PONG to its own PING)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_ManyIdentitiesFromOneSlash24_When_TheyPing_Then_TableHoldsNoMoreThanTheTableIpLimit()
        {
            using var listener = StartListener();
            using var attacker = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            for (var i = 0; i < 60; i++)
                await attacker.SendPingAsync(target, EthECKey.GenerateKey());
            await Task.Delay(1500);

            var fromSubnet = listener.Routing.AllNodes().Count(n => n.IP.Equals(IPAddress.Loopback));
            Assert.True(fromSubnet <= 10,
                $"{fromSubnet} routing-table entries come from one /24 (geth discover/table.go:57 tableIPLimit=10 per /24, bucketIPLimit=2)");
        }
    }
}
