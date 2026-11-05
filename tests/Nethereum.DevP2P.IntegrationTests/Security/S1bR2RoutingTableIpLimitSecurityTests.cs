using System.Linq;
using System.Net;
using Nethereum.DevP2P.Discv4;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2RoutingTableIpLimitSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public void Given_ManyDiscv4NodesFromOneSlash24_When_Added_Then_TableHoldsNoMoreThanTheTableIpLimit()
        {
            var table = new Discv4RoutingTable(EthECKey.GenerateKey().GetPubKeyNoPrefix());

            for (var i = 0; i < 200; i++)
            {
                table.Add(new Discv4Node
                {
                    NodeId = EthECKey.GenerateKey().GetPubKeyNoPrefix(),
                    IP = IPAddress.Parse("8.8.4." + (i % 250 + 1)),
                    UdpPort = 30303,
                    TcpPort = 30303
                });
            }

            Assert.True(table.Count <= Discv4RoutingTable.TableIpLimit, $"{table.Count} nodes from one /24 sit in the table");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_ManyDiscv4NodesFromDistinctSubnets_When_Added_Then_TheyAreAdmitted()
        {
            var table = new Discv4RoutingTable(EthECKey.GenerateKey().GetPubKeyNoPrefix());

            for (var i = 0; i < 30; i++)
            {
                table.Add(new Discv4Node
                {
                    NodeId = EthECKey.GenerateKey().GetPubKeyNoPrefix(),
                    IP = IPAddress.Parse("8." + (i + 10) + ".4.4"),
                    UdpPort = 30303,
                    TcpPort = 30303
                });
            }

            Assert.True(table.Count >= 25, $"only {table.Count} of 30 nodes from distinct /24 subnets were admitted");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_ManyDiscv5EntriesFromOneSlash24_When_Upserted_Then_TableHoldsNoMoreThanTheTableIpLimit()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);

            for (var i = 0; i < 200; i++)
            {
                table.Upsert(new Discv5RoutingTable.Entry
                {
                    NodeId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix()),
                    Address = new IPEndPoint(IPAddress.Parse("8.8.4." + (i % 250 + 1)), 30303),
                    EnrEncoded = new byte[] { 0xc0 }
                });
            }

            Assert.True(table.Count <= Discv5RoutingTable.TableIpLimit, $"{table.Count} entries from one /24 sit in the table");
        }
    }
}
