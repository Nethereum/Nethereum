using System;
using System.Net;
using Nethereum.DevP2P.Discv4;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv4
{
    /// <summary>
    /// Tests for the discv4 packet wire format:
    /// [hash(32) || signature(65) || type(1) || data]
    /// hash = keccak256(signature || type || data)
    /// signature = ECDSA(keccak256(type || data))
    /// </summary>
    public class Discv4PacketTests
    {
        [Fact]
        public void EncodeDecode_PingPacket_RecoversSenderPubKey()
        {
            var senderKey = EthECKey.GenerateKey();

            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30303, TcpPort = 30303 },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30304, TcpPort = 0 },
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            var pingData = Discv4MessageEncoder.EncodePing(ping);

            var packet = Discv4Packet.Encode(senderKey, Discv4MessageType.Ping, pingData);

            Assert.True(packet.Length >= Discv4Packet.HeaderLength);
            Assert.True(packet.Length <= Discv4Packet.MaxPacketSize);

            var decoded = Discv4Packet.Decode(packet);
            Assert.Equal(Discv4MessageType.Ping, decoded.Type);
            Assert.Equal(senderKey.GetPubKeyNoPrefix().ToHex(), decoded.SenderPubKey.ToHex());

            var decodedPing = Discv4MessageEncoder.DecodePing(decoded.Data);
            Assert.Equal(ping.Expiration, decodedPing.Expiration);
        }

        [Fact]
        public void EncodeDecode_FindNodePacket_RecoversSenderPubKey()
        {
            var senderKey = EthECKey.GenerateKey();

            var target = new byte[64];
            new Random(0xC0DE).NextBytes(target);

            var findNode = new Discv4FindNodeMessage
            {
                Target = target,
                Expiration = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds()
            };
            var data = Discv4MessageEncoder.EncodeFindNode(findNode);

            var packet = Discv4Packet.Encode(senderKey, Discv4MessageType.FindNode, data);
            var decoded = Discv4Packet.Decode(packet);

            Assert.Equal(Discv4MessageType.FindNode, decoded.Type);
            Assert.Equal(senderKey.GetPubKeyNoPrefix().ToHex(), decoded.SenderPubKey.ToHex());
        }

        [Fact]
        public void Decode_PacketWithTamperedData_ThrowsHashMismatch()
        {
            var key = EthECKey.GenerateKey();
            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30303 },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30304 },
                Expiration = 1700000000
            };
            var packet = Discv4Packet.Encode(key, Discv4MessageType.Ping, Discv4MessageEncoder.EncodePing(ping));

            packet[Discv4Packet.HeaderLength + 5] ^= 0xFF;

            Assert.Throws<InvalidOperationException>(() => Discv4Packet.Decode(packet));
        }

        [Fact]
        public void Decode_PacketWithTamperedSignature_RecoversWrongKey()
        {
            var senderKey = EthECKey.GenerateKey();
            var ping = new Discv4PingMessage
            {
                Version = 4,
                From = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30303 },
                To = new Discv4Endpoint { IP = IPAddress.Loopback, UdpPort = 30304 },
                Expiration = 1700000000
            };
            var packet = Discv4Packet.Encode(senderKey, Discv4MessageType.Ping, Discv4MessageEncoder.EncodePing(ping));

            packet[Discv4Packet.HashLength + 10] ^= 0xFF;
            Assert.ThrowsAny<Exception>(() => Discv4Packet.Decode(packet));
        }

        [Fact]
        public void MaxPacketSize_Is1280Bytes_PerSpec()
        {
            Assert.Equal(1280, Discv4Packet.MaxPacketSize);
        }

        [Fact]
        public void PacketHeader_Is98Bytes_PerSpec()
        {
            Assert.Equal(32 + 65 + 1, Discv4Packet.HeaderLength);
        }
    }
}
