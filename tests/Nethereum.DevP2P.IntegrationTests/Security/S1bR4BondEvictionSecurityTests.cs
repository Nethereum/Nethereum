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
    [Trait("Category", "Security")]
    public class S1bR4BondEvictionSecurityTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private static EthECKey KeyInTheFarBucket(byte[] localId)
        {
            var localHash = Keccak.CalculateHash(localId);
            while (true)
            {
                var key = EthECKey.GenerateKey();
                if (((Keccak.CalculateHash(key.GetPubKeyNoPrefix())[0] ^ localHash[0]) & 0x80) != 0) return key;
            }
        }

        [Fact]
        public Task Given_AnHonestBondedPeerOutsideTheRoutingTable_When_NoOneElseBonds_Then_ItsFindNodeIsAnswered()
        {
            return FindNodeAnsweredAfterAttackerBonds(0);
        }

        [Fact]
        public Task Given_AnHonestBondedPeerOutsideTheRoutingTable_When_AttackerIdentitiesFillTheBondMap_Then_ItsFindNodeIsStillAnswered()
        {
            return FindNodeAnsweredAfterAttackerBonds(3);
        }

        private static async Task FindNodeAnsweredAfterAttackerBonds(int attackers)
        {
            var key = EthECKey.GenerateKey();
            var table = new Discv4RoutingTable(key.GetPubKeyNoPrefix());
            for (var i = 0; i < Discv4RoutingTable.BucketSize; i++)
            {
                table.Add(new Discv4Node
                {
                    NodeId = KeyInTheFarBucket(key.GetPubKeyNoPrefix()).GetPubKeyNoPrefix(),
                    IP = IPAddress.Parse("8." + (i + 10) + ".4.4"),
                    UdpPort = 30303,
                    TcpPort = 30303
                });
            }
            using var listener = new Discv4Listener(key, table)
            {
                RevalidationInterval = TimeSpan.Zero,
                MaxBondedPeers = 3
            };
            listener.Start(0, IPAddress.Loopback);
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            using var honest = new Discv4RawPeer(KeyInTheFarBucket(key.GetPubKeyNoPrefix()));
            var bonded = false;
            for (var attempt = 0; attempt < 3 && !bonded; attempt++)
            {
                await honest.BondAsync(target);
                bonded = await FindNodeIsAnsweredAsync(honest, target);
            }
            Assert.True(bonded, "the honest peer never completed a bond with the listener");

            for (var i = 0; i < attackers; i++)
            {
                using var attacker = new Discv4RawPeer();
                await attacker.BondAsync(target);
            }

            Assert.True(await FindNodeIsAnsweredAsync(honest, target));
        }

        private static async Task<bool> FindNodeIsAnsweredAsync(Discv4RawPeer peer, IPEndPoint target)
        {
            var findNode = Discv4Packet.Encode(peer.Key, Discv4MessageType.FindNode, Discv4MessageEncoder.EncodeFindNode(new Discv4FindNodeMessage
            {
                Target = new byte[64],
                Expiration = Discv4RawPeer.Future()
            }));
            await peer.SendAsync(findNode, target);
            var replies = await peer.CollectAsync(TimeSpan.FromSeconds(1));
            return replies.Any(r => r.Packet.Type == Discv4MessageType.Neighbors);
        }
    }
}
