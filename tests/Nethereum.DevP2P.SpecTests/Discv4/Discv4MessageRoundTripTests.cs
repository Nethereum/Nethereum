using System.Collections.Generic;
using System.Net;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv4
{
    public class Discv4MessageRoundTripTests
    {
        [Fact]
        public void Ping_NoEnrSeq_RoundTrip()
        {
            var msg = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Parse("192.168.1.1"), UdpPort = 30303, TcpPort = 30303 },
                To = new Discv4Endpoint { IP = IPAddress.Parse("10.0.0.1"), UdpPort = 30303, TcpPort = 0 },
                Expiration = 1700000000
            };

            var bytes = Discv4MessageEncoder.EncodePing(msg);
            var decoded = Discv4MessageEncoder.DecodePing(bytes);

            Assert.Equal(msg.Version, decoded.Version);
            Assert.Equal(msg.From.IP, decoded.From.IP);
            Assert.Equal(msg.From.UdpPort, decoded.From.UdpPort);
            Assert.Equal(msg.From.TcpPort, decoded.From.TcpPort);
            Assert.Equal(msg.To.IP, decoded.To.IP);
            Assert.Equal(msg.To.UdpPort, decoded.To.UdpPort);
            Assert.Equal(msg.Expiration, decoded.Expiration);
            Assert.Null(decoded.EnrSeq);
        }

        [Fact]
        public void Ping_WithEnrSeq_RoundTrip()
        {
            var msg = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30303, TcpPort = 30303 },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30304, TcpPort = 0 },
                Expiration = 1700000000,
                EnrSeq = 42
            };

            var bytes = Discv4MessageEncoder.EncodePing(msg);
            var decoded = Discv4MessageEncoder.DecodePing(bytes);

            Assert.Equal(42ul, decoded.EnrSeq);
        }

        [Fact]
        public void Pong_RoundTrip()
        {
            var pingHash = new byte[32];
            for (int i = 0; i < 32; i++) pingHash[i] = (byte)(0xAB ^ i);

            var msg = new Discv4PongMessage
            {
                To = new Discv4Endpoint { IP = IPAddress.Parse("203.0.113.1"), UdpPort = 30303, TcpPort = 30303 },
                PingHash = pingHash,
                Expiration = 1700000060,
                EnrSeq = 7
            };

            var bytes = Discv4MessageEncoder.EncodePong(msg);
            var decoded = Discv4MessageEncoder.DecodePong(bytes);

            Assert.Equal(msg.To.IP, decoded.To.IP);
            Assert.Equal(msg.PingHash.ToHex(), decoded.PingHash.ToHex());
            Assert.Equal(msg.Expiration, decoded.Expiration);
            Assert.Equal(7ul, decoded.EnrSeq);
        }

        [Fact]
        public void FindNode_RoundTrip()
        {
            var target = new byte[64];
            for (int i = 0; i < 64; i++) target[i] = (byte)(0x77 + i);

            var msg = new Discv4FindNodeMessage
            {
                Target = target,
                Expiration = 1700000120
            };

            var bytes = Discv4MessageEncoder.EncodeFindNode(msg);
            var decoded = Discv4MessageEncoder.DecodeFindNode(bytes);

            Assert.Equal(target.ToHex(), decoded.Target.ToHex());
            Assert.Equal(msg.Expiration, decoded.Expiration);
        }

        [Fact]
        public void Neighbors_RoundTrip_PreservesAllNodes()
        {
            var nodeId1 = new byte[64];
            var nodeId2 = new byte[64];
            for (int i = 0; i < 64; i++) { nodeId1[i] = (byte)(0x11 + i); nodeId2[i] = (byte)(0x22 + i); }

            var msg = new Discv4NeighborsMessage
            {
                Nodes = new List<Discv4Neighbor>
                {
                    new() { IP = IPAddress.Parse("198.51.100.1"), UdpPort = 30303, TcpPort = 30303, NodeId = nodeId1 },
                    new() { IP = IPAddress.Parse("198.51.100.2"), UdpPort = 30304, TcpPort = 30304, NodeId = nodeId2 }
                },
                Expiration = 1700000180
            };

            var bytes = Discv4MessageEncoder.EncodeNeighbors(msg);
            var decoded = Discv4MessageEncoder.DecodeNeighbors(bytes);

            Assert.Equal(msg.Expiration, decoded.Expiration);
            Assert.Equal(2, decoded.Nodes.Count);
            Assert.Equal(msg.Nodes[0].IP, decoded.Nodes[0].IP);
            Assert.Equal(msg.Nodes[0].UdpPort, decoded.Nodes[0].UdpPort);
            Assert.Equal(msg.Nodes[0].NodeId.ToHex(), decoded.Nodes[0].NodeId.ToHex());
            Assert.Equal(msg.Nodes[1].UdpPort, decoded.Nodes[1].UdpPort);
        }

        [Fact]
        public void Neighbors_Empty_RoundTrip()
        {
            var msg = new Discv4NeighborsMessage { Expiration = 1700000000 };
            var bytes = Discv4MessageEncoder.EncodeNeighbors(msg);
            var decoded = Discv4MessageEncoder.DecodeNeighbors(bytes);
            Assert.Empty(decoded.Nodes);
            Assert.Equal(1700000000L, decoded.Expiration);
        }
    }
}
