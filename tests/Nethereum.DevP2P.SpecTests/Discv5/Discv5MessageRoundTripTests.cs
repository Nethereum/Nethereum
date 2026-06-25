using System.Collections.Generic;
using Nethereum.DevP2P.Discv5;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5MessageRoundTripTests
    {
        [Fact]
        public void Ping_RoundTrip()
        {
            var m = new Discv5PingMessage
            {
                RequestId = new byte[] { 0x01, 0x02, 0x03 },
                EnrSeq = 42
            };
            var packed = Discv5MessageEncoder.EncodePing(m);
            var (type, body) = Discv5MessageEncoder.Unpack(packed);
            Assert.Equal(Discv5MessageType.Ping, type);

            var d = Discv5MessageEncoder.DecodePing(body);
            Assert.Equal(m.RequestId.ToHex(), d.RequestId.ToHex());
            Assert.Equal(m.EnrSeq, d.EnrSeq);
        }

        [Fact]
        public void Pong_RoundTrip_IPv4()
        {
            var m = new Discv5PongMessage
            {
                RequestId = new byte[] { 0xAA },
                EnrSeq = 7,
                RecipientIp = new byte[] { 192, 168, 1, 100 },
                RecipientPort = 30303
            };
            var packed = Discv5MessageEncoder.EncodePong(m);
            var (type, body) = Discv5MessageEncoder.Unpack(packed);
            Assert.Equal(Discv5MessageType.Pong, type);

            var d = Discv5MessageEncoder.DecodePong(body);
            Assert.Equal(m.RecipientIp.ToHex(), d.RecipientIp.ToHex());
            Assert.Equal(m.RecipientPort, d.RecipientPort);
        }

        [Fact]
        public void Pong_RoundTrip_IPv6()
        {
            var ipv6 = new byte[16];
            for (int i = 0; i < 16; i++) ipv6[i] = (byte)(0x20 + i);
            var m = new Discv5PongMessage
            {
                RequestId = new byte[] { 0xFF },
                EnrSeq = 99,
                RecipientIp = ipv6,
                RecipientPort = 9000
            };
            var d = Discv5MessageEncoder.DecodePong(Discv5MessageEncoder.Unpack(Discv5MessageEncoder.EncodePong(m)).body);
            Assert.Equal(16, d.RecipientIp.Length);
            Assert.Equal(9000, d.RecipientPort);
        }

        [Fact]
        public void FindNode_RoundTrip()
        {
            var m = new Discv5FindNodeMessage
            {
                RequestId = new byte[] { 0x01 },
                Distances = new List<ulong> { 0, 256, 255, 200 }
            };
            var packed = Discv5MessageEncoder.EncodeFindNode(m);
            var (type, body) = Discv5MessageEncoder.Unpack(packed);
            Assert.Equal(Discv5MessageType.FindNode, type);

            var d = Discv5MessageEncoder.DecodeFindNode(body);
            Assert.Equal(m.Distances, d.Distances);
        }

        [Fact]
        public void Nodes_RoundTrip_WithEnrPayload()
        {
            var enrA = new byte[] { 0xC0 };
            var enrB = new byte[] { 0xC1, 0x80 };
            var m = new Discv5NodesMessage
            {
                RequestId = new byte[] { 0x02 },
                Total = 2,
                Records = new List<byte[]> { enrA, enrB }
            };
            var packed = Discv5MessageEncoder.EncodeNodes(m);
            var (type, body) = Discv5MessageEncoder.Unpack(packed);
            Assert.Equal(Discv5MessageType.Nodes, type);

            var d = Discv5MessageEncoder.DecodeNodes(body);
            Assert.Equal((byte)2, d.Total);
            Assert.Equal(2, d.Records.Count);
        }

        [Fact]
        public void TalkReq_RoundTrip()
        {
            var m = new Discv5TalkReqMessage
            {
                RequestId = new byte[] { 0x03 },
                Protocol = System.Text.Encoding.ASCII.GetBytes("portal"),
                Request = new byte[] { 0x01, 0x02, 0x03 }
            };
            var packed = Discv5MessageEncoder.EncodeTalkReq(m);
            var (type, body) = Discv5MessageEncoder.Unpack(packed);
            Assert.Equal(Discv5MessageType.TalkReq, type);

            var d = Discv5MessageEncoder.DecodeTalkReq(body);
            Assert.Equal("portal", System.Text.Encoding.ASCII.GetString(d.Protocol));
            Assert.Equal(m.Request.ToHex(), d.Request.ToHex());
        }

        [Fact]
        public void TalkResp_RoundTrip()
        {
            var m = new Discv5TalkRespMessage
            {
                RequestId = new byte[] { 0x04 },
                Response = new byte[] { 0xAA, 0xBB, 0xCC }
            };
            var d = Discv5MessageEncoder.DecodeTalkResp(
                Discv5MessageEncoder.Unpack(Discv5MessageEncoder.EncodeTalkResp(m)).body);
            Assert.Equal(m.Response.ToHex(), d.Response.ToHex());
        }

        [Fact]
        public void MessageTypeByte_IsFirstByteOfPackedMessage()
        {
            var ping = Discv5MessageEncoder.EncodePing(new Discv5PingMessage { RequestId = new byte[] { 0x01 }, EnrSeq = 1 });
            Assert.Equal((byte)Discv5MessageType.Ping, ping[0]);

            var pong = Discv5MessageEncoder.EncodePong(new Discv5PongMessage
            {
                RequestId = new byte[] { 0x01 },
                RecipientIp = new byte[] { 127, 0, 0, 1 },
                RecipientPort = 30303
            });
            Assert.Equal((byte)Discv5MessageType.Pong, pong[0]);
        }
    }
}
