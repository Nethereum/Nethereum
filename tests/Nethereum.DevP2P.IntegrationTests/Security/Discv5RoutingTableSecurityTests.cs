using System.Collections.Generic;
using System.Linq;
using System.Net;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv5RoutingTableSecurityTests
    {
        private static List<(EthECKey Key, byte[] NodeId)> NodesAtDistance256(byte[] localId, int count)
        {
            var nodes = new List<(EthECKey, byte[])>();
            while (nodes.Count < count)
            {
                var key = EthECKey.GenerateKey();
                var id = Discv5Crypto.ComputeNodeId(key.GetPubKeyNoPrefix());
                if (Discv5RoutingTable.LogDistance(localId, id) == 256) nodes.Add((key, id));
            }
            return nodes;
        }

        private static Discv5RoutingTable.Entry Entry((EthECKey Key, byte[] NodeId) node, string ip, ulong seq = 1)
            => new Discv5RoutingTable.Entry
            {
                NodeId = node.NodeId,
                Address = new IPEndPoint(IPAddress.Parse(ip), 30303),
                EnrEncoded = Discv5RawPeer.SignEnr(node.Key, IPAddress.Parse(ip), 30303, seq)
            };

        [Fact]
        [Trait("Category", "Security")]
        public void Given_FullBucketOfEstablishedNodes_When_NewIdentitiesAreUpserted_Then_EstablishedNodesAreNotEvicted()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var nodes = NodesAtDistance256(localId, 32);
            var established = nodes.Take(16).ToList();
            var newcomers = nodes.Skip(16).ToList();
            for (var i = 0; i < established.Count; i++)
                table.Upsert(Entry(established[i], $"8.{i + 1}.0.1"));

            for (var i = 0; i < newcomers.Count; i++)
                table.Upsert(Entry(newcomers[i], $"9.{i + 1}.0.1"));

            var kept = established.Count(e => table.AtDistance(256).Any(x => x.NodeId.SequenceEqual(e.NodeId)));
            Assert.True(kept == established.Count,
                $"{established.Count - kept} of {established.Count} established nodes were displaced by newcomers (geth keeps live entries; new nodes wait in the replacement list, discover/table.go)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_ManyIdentitiesFromOneSlash24_When_Upserted_Then_BucketHoldsNoMoreThanTheBucketIpLimit()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var nodes = NodesAtDistance256(localId, 16);

            for (var i = 0; i < nodes.Count; i++)
                table.Upsert(Entry(nodes[i], $"203.0.113.{i + 1}"));

            var count = table.AtDistance(256).Count;
            Assert.True(count <= 2,
                $"{count} entries from one /24 sit in one bucket (geth discover/table.go:56 bucketIPLimit=2 per /24, tableIPLimit=10)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_NewerEnrInTable_When_AnOlderSequenceOfTheSameNodeIsUpserted_Then_TheNewerRecordIsKept()
        {
            var localId = Discv5Crypto.ComputeNodeId(EthECKey.GenerateKey().GetPubKeyNoPrefix());
            var table = new Discv5RoutingTable(localId);
            var node = NodesAtDistance256(localId, 1)[0];
            var newer = Entry(node, "8.8.4.4", seq: 10);
            var older = Entry(node, "1.2.3.4", seq: 5);

            table.Upsert(newer);
            table.Upsert(older);

            var stored = table.AtDistance(256).Single();
            Assert.True(stored.EnrEncoded.SequenceEqual(newer.EnrEncoded),
                "an ENR with sequence 5 replaced the stored sequence 10 record (EIP-778: the higher sequence number wins)");
        }
    }
}
