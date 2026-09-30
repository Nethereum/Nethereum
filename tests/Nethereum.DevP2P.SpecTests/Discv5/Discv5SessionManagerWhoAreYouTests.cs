using System.Net;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5SessionManagerWhoAreYouTests
    {
        [Fact]
        public void Given_PendingChallenge_When_PeerResendsOrdinary_Then_SameWhoAreYouBytesReturned()
        {
            var localKey = EthECKey.GenerateKey();
            var sessionMgr = new Discv5SessionManager(localKey);

            var peerId = new byte[32];
            for (int i = 0; i < peerId.Length; i++) peerId[i] = (byte)(0xAA ^ i);
            var peerAddr = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 30303);

            var first = SendUndecryptableOrdinary(sessionMgr, peerId, peerAddr);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, first.Kind);

            var second = SendUndecryptableOrdinary(sessionMgr, peerId, peerAddr);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, second.Kind);

            Assert.NotNull(first.OutgoingBytes);
            Assert.NotNull(second.OutgoingBytes);
            Assert.Equal(first.OutgoingBytes, second.OutgoingBytes);
        }

        [Fact]
        public void Given_SamePeerWithFreshNonce_When_SecondOrdinaryArrives_Then_FreshWhoAreYouIssued()
        {
            var localKey = EthECKey.GenerateKey();
            var sessionMgr = new Discv5SessionManager(localKey);

            var peerId = new byte[32];
            for (int i = 0; i < peerId.Length; i++) peerId[i] = (byte)(0xAA ^ i);
            var peerAddr = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 30303);

            var firstNonce = new byte[Discv5Packet.NonceLength];
            for (int i = 0; i < firstNonce.Length; i++) firstNonce[i] = (byte)(0x11 + i);
            var secondNonce = new byte[Discv5Packet.NonceLength];
            for (int i = 0; i < secondNonce.Length; i++) secondNonce[i] = (byte)(0x22 + i);

            var first = SendUndecryptableOrdinaryWithNonce(sessionMgr, peerId, peerAddr, firstNonce);
            var second = SendUndecryptableOrdinaryWithNonce(sessionMgr, peerId, peerAddr, secondNonce);

            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, first.Kind);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, second.Kind);
            Assert.NotEqual(first.OutgoingBytes, second.OutgoingBytes);
        }

        [Fact]
        public void Given_DifferentPeers_When_BothSendOrdinary_Then_DistinctChallengesIssued()
        {
            var localKey = EthECKey.GenerateKey();
            var sessionMgr = new Discv5SessionManager(localKey);

            var peerIdA = new byte[32];
            for (int i = 0; i < peerIdA.Length; i++) peerIdA[i] = (byte)(0xAA ^ i);
            var peerIdB = new byte[32];
            for (int i = 0; i < peerIdB.Length; i++) peerIdB[i] = (byte)(0xBB ^ i);

            var addr = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 30303);

            var fromA = SendUndecryptableOrdinary(sessionMgr, peerIdA, addr);
            var fromB = SendUndecryptableOrdinary(sessionMgr, peerIdB, addr);

            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, fromA.Kind);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, fromB.Kind);
            Assert.NotEqual(fromA.OutgoingBytes, fromB.OutgoingBytes);
        }

        private static Discv5SessionManager.IncomingPacket SendUndecryptableOrdinary(
            Discv5SessionManager mgr, byte[] srcId, IPEndPoint fromAddr)
        {
            var nonce = new byte[Discv5Packet.NonceLength];
            for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(0x11 + i);
            return SendUndecryptableOrdinaryWithNonce(mgr, srcId, fromAddr, nonce);
        }

        private static Discv5SessionManager.IncomingPacket SendUndecryptableOrdinaryWithNonce(
            Discv5SessionManager mgr, byte[] srcId, IPEndPoint fromAddr, byte[] nonce)
        {
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            for (int i = 0; i < maskingIv.Length; i++) maskingIv[i] = (byte)(0x33 + i);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = nonce,
                AuthData = srcId
            };
            var junk = new byte[32];
            for (int i = 0; i < junk.Length; i++) junk[i] = 0xCC;

            var packet = Discv5Packet.EncodePacket(maskingIv, header, mgr.LocalNodeId, junk);
            return mgr.Process(packet, fromAddr);
        }
    }
}
