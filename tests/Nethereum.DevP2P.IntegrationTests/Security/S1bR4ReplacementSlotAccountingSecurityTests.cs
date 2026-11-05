using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.DevP2P.Discv4;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR4ReplacementSlotAccountingSecurityTests
    {
        private static readonly Sha3Keccack Keccak = new Sha3Keccack();

        private static List<Discv4Node> Discv4NodesInTheFarBucket(byte[] localId, int count)
        {
            var localHash = Keccak.CalculateHash(localId);
            var nodes = new List<Discv4Node>();
            while (nodes.Count < count)
            {
                var id = EthECKey.GenerateKey().GetPubKeyNoPrefix();
                if (((Keccak.CalculateHash(id)[0] ^ localHash[0]) & 0x80) == 0) continue;
                nodes.Add(new Discv4Node
                {
                    NodeId = id,
                    IP = IPAddress.Parse("8." + (nodes.Count + 10) + ".4.4"),
                    UdpPort = 30303,
                    TcpPort = 30303
                });
            }
            return nodes;
        }

        private static List<Discv5RoutingTable.Entry> Discv5EntriesInTheFarBucket(byte[] localId, int count)
        {
            var entries = new List<Discv5RoutingTable.Entry>();
            while (entries.Count < count)
            {
                var id = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
                if (((id[0] ^ localId[0]) & 0x80) == 0) continue;
                entries.Add(new Discv5RoutingTable.Entry
                {
                    NodeId = id,
                    Address = new IPEndPoint(IPAddress.Parse("8." + (entries.Count + 10) + ".4.4"), 30303),
                    EnrEncoded = new byte[] { 0xc0 }
                });
            }
            return entries;
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Discv4ReplacementsAtTheSubnetLimit_When_OneIsRemoved_Then_AnotherFromTheSubnetIsAccepted()
        {
            var localId = EthECKey.GenerateKey().GetPubKeyNoPrefix();
            var table = new Discv4RoutingTable(localId);
            var nodes = Discv4NodesInTheFarBucket(localId, Discv4RoutingTable.BucketSize + 3);
            foreach (var node in nodes.Take(Discv4RoutingTable.BucketSize)) table.Add(node);
            var sybils = nodes.Skip(Discv4RoutingTable.BucketSize).ToList();
            for (var i = 0; i < sybils.Count; i++) sybils[i].IP = IPAddress.Parse("9.9.9." + (i + 1));
            table.Add(sybils[0]);
            table.Add(sybils[1]);
            table.Remove(sybils[0].NodeId);

            table.Add(sybils[2]);

            Assert.Equal(Discv4RoutingTable.BucketIpLimit, table.ReplacementsFor(sybils[0].NodeId).Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Discv4ReplacementsPromotedAndRemoved_When_TheSubnetIsFilledAgain_Then_ExactlyTheBucketLimitIsAccepted()
        {
            var localId = EthECKey.GenerateKey().GetPubKeyNoPrefix();
            var table = new Discv4RoutingTable(localId);
            var nodes = Discv4NodesInTheFarBucket(localId, Discv4RoutingTable.BucketSize + 5);
            foreach (var node in nodes.Take(Discv4RoutingTable.BucketSize)) table.Add(node);
            var sybils = nodes.Skip(Discv4RoutingTable.BucketSize).ToList();
            for (var i = 0; i < sybils.Count; i++) sybils[i].IP = IPAddress.Parse("9.9.9." + (i + 1));
            table.Add(sybils[0]);
            table.Add(sybils[1]);
            table.Remove(nodes[0].NodeId);
            table.Remove(sybils[0].NodeId);
            table.Remove(sybils[1].NodeId);
            table.Remove(nodes[1].NodeId);
            var before = table.Count;

            table.Add(sybils[2]);
            table.Add(sybils[3]);
            table.Add(sybils[4]);

            Assert.Equal(before + Discv4RoutingTable.BucketIpLimit, table.Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Discv5ReplacementsAtTheSubnetLimit_When_OneIsRemoved_Then_AnotherFromTheSubnetIsAccepted()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var entries = Discv5EntriesInTheFarBucket(localId, table.BucketCapacity + 3);
            foreach (var entry in entries.Take(table.BucketCapacity)) table.Upsert(entry);
            var sybils = entries.Skip(table.BucketCapacity).ToList();
            for (var i = 0; i < sybils.Count; i++) sybils[i].Address = new IPEndPoint(IPAddress.Parse("9.9.9." + (i + 1)), 30303);
            table.Upsert(sybils[0]);
            table.Upsert(sybils[1]);
            table.Remove(sybils[0].NodeId);

            table.Upsert(sybils[2]);

            var distance = Discv5RoutingTable.LogDistance(localId, sybils[0].NodeId);
            Assert.Equal(Discv5RoutingTable.BucketIpLimit, table.ReplacementsAtDistance(distance).Count);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_Discv5ReplacementsPromotedAndRemoved_When_TheSubnetIsFilledAgain_Then_ExactlyTheBucketLimitIsAccepted()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var entries = Discv5EntriesInTheFarBucket(localId, table.BucketCapacity + 5);
            foreach (var entry in entries.Take(table.BucketCapacity)) table.Upsert(entry);
            var sybils = entries.Skip(table.BucketCapacity).ToList();
            for (var i = 0; i < sybils.Count; i++) sybils[i].Address = new IPEndPoint(IPAddress.Parse("9.9.9." + (i + 1)), 30303);
            table.Upsert(sybils[0]);
            table.Upsert(sybils[1]);
            table.Remove(entries[0].NodeId);
            table.Remove(sybils[0].NodeId);
            table.Remove(sybils[1].NodeId);
            table.Remove(entries[1].NodeId);
            var before = table.Count;

            table.Upsert(sybils[2]);
            table.Upsert(sybils[3]);
            table.Upsert(sybils[4]);

            Assert.Equal(before + Discv5RoutingTable.BucketIpLimit, table.Count);
        }
    }
}
