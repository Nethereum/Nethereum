using System;
using System.Security.Cryptography;
using Nethereum.DevP2P.Discv5;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Discv5
{
    public class Discv5PacketTests
    {
        [Fact]
        public void Header_MaskAndUnmask_RoundTrip_OrdinaryPacket()
        {
            var localNodeId = Fill(32, 0xAA);

            var maskingIv = Fill(16, 0x11);
            var srcNodeId = Fill(32, 0x55);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = Fill(12, 0x77),
                AuthData = srcNodeId
            };

            var packet = Discv5Packet.EncodePacket(maskingIv, header, destNodeId: localNodeId, encryptedMessage: new byte[0]);

            Assert.True(packet.Length >= Discv5Packet.MaskingIvLength + Discv5Packet.HeaderStaticLength + 32);

            var (decodedIv, decodedHeader, _, _) = Discv5Packet.DecodePacket(packet, localNodeId);
            Assert.Equal(maskingIv.ToHex(), decodedIv.ToHex());
            Assert.Equal(Discv5Packet.PacketFlag.Ordinary, decodedHeader.Flag);
            Assert.Equal(header.Nonce.ToHex(), decodedHeader.Nonce.ToHex());
            Assert.Equal(srcNodeId.ToHex(), decodedHeader.AuthData.ToHex());
        }

        [Fact]
        public void Header_MaskAndUnmask_RoundTrip_WhoAreYouPacket()
        {
            var localNodeId = Fill(32, 0x33);
            var maskingIv = Fill(16, 0x99);

            var whoAreYouAuth = new byte[24];
            for (int i = 0; i < 24; i++) whoAreYouAuth[i] = (byte)(0xC0 + i);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.WhoAreYou,
                Nonce = Fill(12, 0xAB),
                AuthData = whoAreYouAuth
            };

            var packet = Discv5Packet.EncodePacket(maskingIv, header, localNodeId, new byte[0]);
            var (_, decodedHeader, _, _) = Discv5Packet.DecodePacket(packet, localNodeId);

            Assert.Equal(Discv5Packet.PacketFlag.WhoAreYou, decodedHeader.Flag);
            Assert.Equal(whoAreYouAuth.ToHex(), decodedHeader.AuthData.ToHex());
        }

        [Fact]
        public void WrongMaskingKey_FailsValidation()
        {
            var realLocalId = Fill(32, 0xDD);
            var wrongLocalId = Fill(32, 0xEE);
            var maskingIv = Fill(16, 0x42);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = Fill(12, 0x01),
                AuthData = Fill(32, 0x02)
            };
            var packet = Discv5Packet.EncodePacket(maskingIv, header, realLocalId, new byte[0]);

            Assert.Throws<InvalidOperationException>(() => Discv5Packet.DecodePacket(packet, wrongLocalId));
        }

        [Fact]
        public void Message_AesGcmEncryptDecrypt_RoundTrip()
        {
            var sessionKey = new byte[16];
            RandomNumberGenerator.Fill(sessionKey);
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            var aad = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            var plaintext = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x12, 0x34 };

            var ciphertext = Discv5Packet.EncryptMessage(sessionKey, nonce, aad, plaintext);
            Assert.True(ciphertext.Length == plaintext.Length + 16, "ciphertext should append 16-byte GCM tag");

            var roundTripped = Discv5Packet.DecryptMessage(sessionKey, nonce, aad, ciphertext);
            Assert.Equal(plaintext.ToHex(), roundTripped.ToHex());
        }

        [Fact]
        public void Message_AesGcm_TamperedTag_ThrowsCryptographicException()
        {
            var sessionKey = new byte[16];
            RandomNumberGenerator.Fill(sessionKey);
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            var ciphertext = Discv5Packet.EncryptMessage(sessionKey, nonce, new byte[0], new byte[] { 0xAA, 0xBB });

            ciphertext[ciphertext.Length - 1] ^= 0xFF;

            Assert.ThrowsAny<Exception>(() =>
                Discv5Packet.DecryptMessage(sessionKey, nonce, new byte[0], ciphertext));
        }

        [Fact]
        public void Constants_MatchSpec()
        {
            Assert.Equal(16, Discv5Packet.MaskingIvLength);
            Assert.Equal(12, Discv5Packet.NonceLength);
            Assert.Equal(23, Discv5Packet.HeaderStaticLength);
            Assert.Equal("discv5", System.Text.Encoding.ASCII.GetString(Discv5Packet.ProtocolId));
            Assert.Equal(0x0001, Discv5Packet.Version);
        }

        [Fact]
        public void EndToEnd_OrdinaryPacket_WithEncryptedPing_RoundTrip()
        {
            var localNodeId = Fill(32, 0x12);
            var maskingIv = Fill(16, 0x34);
            var sessionKey = Fill(16, 0x56);
            var nonce = Fill(12, 0x78);
            var srcNodeId = Fill(32, 0x9A);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = nonce,
                AuthData = srcNodeId
            };

            var pingPlaintext = Discv5MessageEncoder.EncodePing(new Discv5PingMessage
            {
                RequestId = new byte[] { 0x01 },
                EnrSeq = 42
            });
            var aad = BuildAad(maskingIv, header);
            var encryptedMessage = Discv5Packet.EncryptMessage(sessionKey, nonce, aad, pingPlaintext);

            var packet = Discv5Packet.EncodePacket(maskingIv, header, localNodeId, encryptedMessage);

            var (decodedIv, decodedHeader, encryptedAgain, _) = Discv5Packet.DecodePacket(packet, localNodeId);
            var aadRebuilt = BuildAad(decodedIv, decodedHeader);
            var decrypted = Discv5Packet.DecryptMessage(sessionKey, decodedHeader.Nonce, aadRebuilt, encryptedAgain);

            var (type, body) = Discv5MessageEncoder.Unpack(decrypted);
            Assert.Equal(Discv5MessageType.Ping, type);
            var ping = Discv5MessageEncoder.DecodePing(body);
            Assert.Equal(42ul, ping.EnrSeq);
        }

        private static byte[] BuildAad(byte[] maskingIv, Discv5Packet.Header header)
        {
            // AAD per spec = masking-iv || static-header || authdata.
            var aad = new byte[Discv5Packet.MaskingIvLength + Discv5Packet.HeaderStaticLength + header.AuthData.Length];
            int o = 0;
            Buffer.BlockCopy(maskingIv, 0, aad, o, Discv5Packet.MaskingIvLength); o += Discv5Packet.MaskingIvLength;
            Buffer.BlockCopy(Discv5Packet.ProtocolId, 0, aad, o, Discv5Packet.ProtocolId.Length); o += Discv5Packet.ProtocolId.Length;
            aad[o++] = (byte)((Discv5Packet.Version >> 8) & 0xff);
            aad[o++] = (byte)(Discv5Packet.Version & 0xff);
            aad[o++] = (byte)header.Flag;
            Buffer.BlockCopy(header.Nonce, 0, aad, o, Discv5Packet.NonceLength); o += Discv5Packet.NonceLength;
            aad[o++] = (byte)((header.AuthData.Length >> 8) & 0xff);
            aad[o++] = (byte)(header.AuthData.Length & 0xff);
            Buffer.BlockCopy(header.AuthData, 0, aad, o, header.AuthData.Length);
            return aad;
        }

        private static byte[] Fill(int len, byte seed)
        {
            var b = new byte[len];
            for (int i = 0; i < len; i++) b[i] = (byte)(seed ^ i);
            return b;
        }
    }
}
