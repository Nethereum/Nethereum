using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv5
{
    public class Discv5SessionManagerWhoAreYouTests
    {
        private const string SpecRule = "discv5-wire §WHOAREYOU nonce-echo";

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", SpecRule)]
        public void Given_PendingOutbound_When_WhoAreYouEchoesCorrectNonce_Then_HandshakeProduced()
        {
            var mgr = new Discv5SessionManager(EthECKey.GenerateKey());
            var (peerNodeId, peerStaticPub, peerAddr) = MakePeer();

            var outboundPacket = mgr.BuildInitialOrdinaryPacket(
                peerNodeId, peerAddr,
                firstMessagePlaintext: new byte[] { 0x01, 0x02, 0x03 },
                peerStaticCompressedPubKey: peerStaticPub,
                localEnrEncoded: Array.Empty<byte>());
            var outboundNonce = ExtractOutboundNonce(outboundPacket, peerNodeId);

            var whoAreYou = BuildWhoAreYouEchoing(outboundNonce, mgr.LocalNodeId);

            var result = mgr.Process(whoAreYou, peerAddr);

            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, result.Kind);
            Assert.NotNull(result.OutgoingBytes);
            Assert.Equal(1, mgr.SessionCount);
            Assert.Equal(0, mgr.PendingOutboundCount);
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", SpecRule)]
        public void Given_PendingOutbound_When_WhoAreYouEchoesWrongNonce_Then_DroppedAndPendingPreserved()
        {
            var mgr = new Discv5SessionManager(EthECKey.GenerateKey());
            var (peerNodeId, peerStaticPub, peerAddr) = MakePeer();

            mgr.BuildInitialOrdinaryPacket(
                peerNodeId, peerAddr,
                firstMessagePlaintext: new byte[] { 0x01, 0x02 },
                peerStaticCompressedPubKey: peerStaticPub);
            Assert.Equal(1, mgr.PendingOutboundCount);

            var bogusNonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(bogusNonce);
            var whoAreYou = BuildWhoAreYouEchoing(bogusNonce, mgr.LocalNodeId);

            var result = mgr.Process(whoAreYou, peerAddr);

            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Ignored, result.Kind);
            Assert.Equal(0, mgr.SessionCount);
            Assert.Equal(1, mgr.PendingOutboundCount);
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", SpecRule)]
        public void Given_NoPendingOutbound_When_WhoAreYouArrives_Then_Ignored()
        {
            var mgr = new Discv5SessionManager(EthECKey.GenerateKey());
            var (_, _, peerAddr) = MakePeer();

            var anyNonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(anyNonce);
            var whoAreYou = BuildWhoAreYouEchoing(anyNonce, mgr.LocalNodeId);

            var result = mgr.Process(whoAreYou, peerAddr);

            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Ignored, result.Kind);
            Assert.Equal(0, mgr.SessionCount);
            Assert.Equal(0, mgr.PendingOutboundCount);
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", SpecRule)]
        public void Given_PendingOutboundToAlice_When_WhoAreYouFromMalloryEchoesCorrectNonce_Then_RejectedByEndpointGuard()
        {
            var mgr = new Discv5SessionManager(EthECKey.GenerateKey());
            var (peerNodeId, peerStaticPub, aliceAddr) = MakePeer();

            var outboundPacket = mgr.BuildInitialOrdinaryPacket(
                peerNodeId, aliceAddr,
                firstMessagePlaintext: new byte[] { 0xAA },
                peerStaticCompressedPubKey: peerStaticPub);
            var outboundNonce = ExtractOutboundNonce(outboundPacket, peerNodeId);

            var malloryAddr = new IPEndPoint(IPAddress.Parse("198.51.100.99"), 30303);
            var whoAreYou = BuildWhoAreYouEchoing(outboundNonce, mgr.LocalNodeId);

            var result = mgr.Process(whoAreYou, malloryAddr);

            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Ignored, result.Kind);
            Assert.Equal(0, mgr.SessionCount);
            Assert.Equal(1, mgr.PendingOutboundCount);
        }

        [Fact]
        [Trait("Category", "Discv5-Security")]
        [Trait("Rule", "NEW-3 bounded pending dictionaries")]
        public void Given_PendingOutboundAtCap_When_FreshDialArrives_Then_OldestEvicted()
        {
            var clockNow = DateTime.UtcNow;
            var mgr = new Discv5SessionManager(EthECKey.GenerateKey(), () => clockNow);

            var oldest = MakePeerWithSeed(0);
            mgr.BuildInitialOrdinaryPacket(
                oldest.NodeId, oldest.Addr,
                firstMessagePlaintext: new byte[] { 0x01 },
                peerStaticCompressedPubKey: oldest.StaticPub);

            clockNow = clockNow.AddHours(1);

            for (int i = 1; i < Discv5SessionManager.MaxPendingOutbound; i++)
            {
                var p = MakePeerWithSeed(i);
                mgr.BuildInitialOrdinaryPacket(
                    p.NodeId, p.Addr,
                    firstMessagePlaintext: new byte[] { (byte)i },
                    peerStaticCompressedPubKey: p.StaticPub);
            }
            Assert.Equal(Discv5SessionManager.MaxPendingOutbound, mgr.PendingOutboundCount);

            var extra = MakePeerWithSeed(int.MaxValue);
            mgr.BuildInitialOrdinaryPacket(
                extra.NodeId, extra.Addr,
                firstMessagePlaintext: new byte[] { 0xFF },
                peerStaticCompressedPubKey: extra.StaticPub);

            Assert.True(mgr.PendingOutboundCount <= Discv5SessionManager.MaxPendingOutbound);
            Assert.False(
                mgr.SnapshotPendingOutbound().Any(
                    info => Nethereum.Util.ByteUtil.AreEqual(info.RemoteNodeId, oldest.NodeId)),
                "the eldest pending dial should have been the evicted entry");
        }

        private static (byte[] NodeId, byte[] StaticPub, IPEndPoint Addr) MakePeer()
        {
            var p = MakePeerWithSeed(7);
            return (p.NodeId, p.StaticPub, p.Addr);
        }

        private static (byte[] NodeId, byte[] StaticPub, IPEndPoint Addr) MakePeerWithSeed(int seed)
        {
            var key = EthECKey.GenerateKey();
            var pub = key.GetPubKey(compresseed: true);
            var nodeId = Discv5Crypto.ComputeNodeId(pub);
            var ip = new IPAddress(new byte[]
            {
                203, 0, 113, (byte)((seed % 250) + 1)
            });
            var port = 30000 + (seed & 0x3FFF);
            return (nodeId, pub, new IPEndPoint(ip, port));
        }

        private static byte[] ExtractOutboundNonce(byte[] outboundPacket, byte[] destNodeId)
        {
            var (_, header, _, _) = Discv5Packet.DecodePacket(outboundPacket, destNodeId);
            return header.Nonce;
        }

        private static byte[] BuildWhoAreYouEchoing(byte[] echoedNonce, byte[] destNodeId)
        {
            var idNonce = new byte[Discv5HandshakePackets.WhoAreYouAuth.IdNonceLength];
            RandomNumberGenerator.Fill(idNonce);
            var authdata = new Discv5HandshakePackets.WhoAreYouAuth
            {
                IdNonce = idNonce,
                EnrSeq = 0
            }.Encode();

            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.WhoAreYou,
                Nonce = echoedNonce,
                AuthData = authdata
            };
            return Discv5Packet.EncodePacket(maskingIv, header, destNodeId, Array.Empty<byte>());
        }
    }
}
