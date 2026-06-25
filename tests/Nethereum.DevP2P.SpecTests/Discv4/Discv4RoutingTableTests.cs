using System;
using System.Net;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv4
{
    public class Discv4RoutingTableTests
    {
        [Fact]
        public void Add_NewNode_IncreasesCount()
        {
            var localId = MakeNodeId(0x11);
            var table = new Discv4RoutingTable(localId);
            Assert.Equal(0, table.Count);

            table.Add(MakeNode(0x22));
            Assert.Equal(1, table.Count);

            table.Add(MakeNode(0x33));
            Assert.Equal(2, table.Count);
        }

        [Fact]
        public void Add_DuplicateNode_DoesNotDouble()
        {
            var table = new Discv4RoutingTable(MakeNodeId(0x11));
            var node = MakeNode(0x22);
            table.Add(node);
            table.Add(node);
            Assert.Equal(1, table.Count);
        }

        [Fact]
        public void Remove_ExistingNode_DecreasesCount()
        {
            var table = new Discv4RoutingTable(MakeNodeId(0x11));
            var node = MakeNode(0x22);
            table.Add(node);
            Assert.Equal(1, table.Count);

            Assert.True(table.Remove(node.NodeId));
            Assert.Equal(0, table.Count);
        }

        [Fact]
        public void FindClosest_OrdersByXorDistanceToTarget()
        {
            var localId = MakeNodeId(0x00);
            var table = new Discv4RoutingTable(localId);

            for (int i = 1; i <= 30; i++)
                table.Add(MakeNode((byte)i));

            var target = MakeNodeId(0x05);
            var closest = table.FindClosest(target, count: 5);

            Assert.Equal(5, closest.Count);
            for (int i = 1; i < closest.Count; i++)
            {
                var d1 = XorDistance(closest[i - 1].NodeId, target);
                var d2 = XorDistance(closest[i].NodeId, target);
                Assert.True(Compare(d1, d2) <= 0,
                    $"FindClosest must return nodes in ascending XOR distance from target");
            }
        }

        [Fact]
        public void BucketSize_MatchesSpec()
        {
            Assert.Equal(16, Discv4RoutingTable.BucketSize);
            Assert.Equal(256, Discv4RoutingTable.BucketCount);
        }

        [Fact]
        public void Add_InvalidNodeIdLength_Rejected()
        {
            var table = new Discv4RoutingTable(MakeNodeId(0x11));
            var bad = new Discv4Node
            {
                NodeId = new byte[32],
                IP = IPAddress.Loopback,
                UdpPort = 30303,
                LastSeen = DateTime.UtcNow
            };
            Assert.False(table.Add(bad));
        }

        private static byte[] MakeNodeId(byte seed)
        {
            var id = new byte[64];
            for (int i = 0; i < 64; i++) id[i] = (byte)(seed ^ i);
            return id;
        }

        private static Discv4Node MakeNode(byte seed)
        {
            return new Discv4Node
            {
                NodeId = MakeNodeId(seed),
                IP = IPAddress.Loopback,
                UdpPort = 30303,
                TcpPort = 30303,
                LastSeen = DateTime.UtcNow
            };
        }

        private static byte[] XorDistance(byte[] a, byte[] b)
        {
            var aHash = new Sha3Keccack().CalculateHash(a);
            var bHash = new Sha3Keccack().CalculateHash(b);
            var dist = new byte[aHash.Length];
            for (int i = 0; i < aHash.Length; i++) dist[i] = (byte)(aHash[i] ^ bHash[i]);
            return dist;
        }

        private static int Compare(byte[] x, byte[] y)
        {
            for (int i = 0; i < x.Length; i++)
            {
                var c = x[i].CompareTo(y[i]);
                if (c != 0) return c;
            }
            return 0;
        }
    }
}
