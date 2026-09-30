using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5SessionManagerEnrVerificationTests
    {
        [Fact]
        public void Given_ValidHandshakeWithIntactEnr_When_Processed_Then_SessionEstablished()
        {
            var (result, _, _, _) = RunHandshake(tamperSignature: false);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Decoded, result.Kind);
            Assert.NotNull(result.Session);
        }

        [Fact]
        public void Given_HandshakeWithTamperedEnrSignature_When_Processed_Then_SessionRejected()
        {
            var (result, _, _, _) = RunHandshake(tamperSignature: true);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Ignored, result.Kind);
            Assert.Null(result.Session);
        }

        [Fact]
        public void Given_HandshakeWithTamperedEnr_When_Processed_Then_ChallengeSurvivesForTheGenuineReply()
        {
            var (result, mgr, _, _) = RunHandshake(tamperSignature: true);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Ignored, result.Kind);
            Assert.Equal(1, mgr.PendingChallengeCount);
        }

        [Fact]
        public void Given_LiveSession_When_UndecryptableOrdinaryArrives_Then_SessionSurvives()
        {
            var (handshake, mgr, peerNodeId, peerAddr) = RunHandshake(tamperSignature: false);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.Decoded, handshake.Kind);
            Assert.Equal(1, mgr.SessionCount);

            TriggerWhoAreYou(mgr, peerNodeId, peerAddr);

            Assert.Equal(1, mgr.SessionCount);
        }

        private static (Discv5SessionManager.IncomingPacket result, Discv5SessionManager mgr, byte[] peerNodeId, IPEndPoint peerAddr) RunHandshake(bool tamperSignature)
        {
            var localKey = EthECKey.GenerateKey();
            var fixedNow = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var sessionMgr = new Discv5SessionManager(localKey, () => fixedNow);

            var peerKey = EthECKey.GenerateKey();
            var peerPubCompressed = peerKey.GetPubKey(compresseed: true);
            var peerNodeId = Discv5Crypto.ComputeNodeId(peerPubCompressed);
            var peerAddr = new IPEndPoint(IPAddress.Parse("203.0.113.42"), 30303);

            var whoAreYouPacket = TriggerWhoAreYou(sessionMgr, peerNodeId, peerAddr);

            var (whoMaskingIv, whoHeader, _, whoRawHeader) =
                Discv5Packet.DecodePacket(whoAreYouPacket, peerNodeId);
            Assert.Equal(Discv5Packet.PacketFlag.WhoAreYou, whoHeader.Flag);
            var challengeData = ConcatBytes(whoMaskingIv, whoRawHeader);

            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["id"] = Encoding.ASCII.GetBytes("v4");
            enr.Pairs["ip"] = new byte[] { 203, 0, 113, 42 };
            EnrRecordSigner.Sign(enr, peerKey);
            if (tamperSignature)
            {
                enr.Signature[0] ^= 0x01;
            }
            var enrEncoded = EnrRecordEncoder.EncodeRecord(enr);

            var peerEphem = EthECKey.GenerateKey();
            var peerEphemPubCompressed = peerEphem.GetPubKey(compresseed: true);
            var idSigInput = Discv5KeyDerivation.ComputeIdSignatureInput(
                challengeData, peerEphemPubCompressed, sessionMgr.LocalNodeId);
            var idSig = Discv5Crypto.SignIdSignature(peerKey, idSigInput);

            var sharedSecret = Discv5Crypto.EcdhCompressed(peerEphem, localKey.GetPubKey(compresseed: true));
            var (initKey, _) = Discv5KeyDerivation.DeriveSessionKeys(
                sharedSecret, challengeData, peerNodeId, sessionMgr.LocalNodeId);

            var pingMsg = new Discv5PingMessage { RequestId = new byte[] { 0x01 }, EnrSeq = 1 };
            var plaintext = Discv5MessageEncoder.EncodePing(pingMsg);

            var handshakeAuth = new Discv5HandshakePackets.HandshakeAuth
            {
                SrcId = peerNodeId,
                IdSignature = idSig,
                EphemeralPubKey = peerEphemPubCompressed,
                Record = enrEncoded
            };
            var nonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(nonce);
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);
            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Handshake,
                Nonce = nonce,
                AuthData = handshakeAuth.Encode()
            };
            var aad = BuildAad(maskingIv, header);
            var encrypted = Discv5Packet.EncryptMessage(initKey, nonce, aad, plaintext);
            var packet = Discv5Packet.EncodePacket(maskingIv, header, sessionMgr.LocalNodeId, encrypted);

            return (sessionMgr.Process(packet, peerAddr), sessionMgr, peerNodeId, peerAddr);
        }

        private static byte[] BuildAad(byte[] maskingIv, Discv5Packet.Header header)
        {
            var aad = new byte[Discv5Packet.MaskingIvLength
                + Discv5Packet.HeaderStaticLength + header.AuthData.Length];
            int o = 0;
            Buffer.BlockCopy(maskingIv, 0, aad, o, Discv5Packet.MaskingIvLength);
            o += Discv5Packet.MaskingIvLength;
            Buffer.BlockCopy(Discv5Packet.ProtocolId, 0, aad, o, Discv5Packet.ProtocolId.Length);
            o += Discv5Packet.ProtocolId.Length;
            aad[o++] = (byte)((Discv5Packet.Version >> 8) & 0xff);
            aad[o++] = (byte)(Discv5Packet.Version & 0xff);
            aad[o++] = (byte)header.Flag;
            Buffer.BlockCopy(header.Nonce, 0, aad, o, Discv5Packet.NonceLength);
            o += Discv5Packet.NonceLength;
            aad[o++] = (byte)((header.AuthData.Length >> 8) & 0xff);
            aad[o++] = (byte)(header.AuthData.Length & 0xff);
            Buffer.BlockCopy(header.AuthData, 0, aad, o, header.AuthData.Length);
            return aad;
        }

        private static byte[] ConcatBytes(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }

        private static byte[] TriggerWhoAreYou(
            Discv5SessionManager mgr, byte[] srcId, IPEndPoint fromAddr)
        {
            var nonce = new byte[Discv5Packet.NonceLength];
            for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(0x11 + i);
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
            var result = mgr.Process(packet, fromAddr);
            Assert.Equal(Discv5SessionManager.IncomingPacketKind.NeedWhoAreYou, result.Kind);
            return result.OutgoingBytes;
        }
    }
}
