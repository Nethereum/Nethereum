using System;
using System.Linq;
using System.Security.Cryptography;
using Nethereum.DevP2P.Crypto;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpxBaseProtocolFrameSecurityTests
    {
        private const int BaseProtocolMaxMsgSize = 2048;

        private static (RlpxSecrets attackerSecrets, RlpxFrameReader victimReader) PairedSecrets()
        {
            var initiator = EthECKey.GenerateKey();
            var recipient = EthECKey.GenerateKey();
            var (auth, state) = RlpxHandshake.CreateAuth(initiator, recipient.GetPubKeyNoPrefix());
            var (ack, _, recipientSecrets) = RlpxHandshake.HandleAuth(recipient, auth);
            var initiatorSecrets = RlpxHandshake.HandleAck(state, ack);
            return (
                initiatorSecrets,
                new RlpxFrameReader(recipientSecrets.AesSecret, recipientSecrets.MacSecret, recipientSecrets.IngressMac));
        }

        private static byte[] ZeroSizeFrame(RlpxSecrets secrets)
        {
            var aes = new AesEngine();
            aes.Init(true, new KeyParameter(secrets.MacSecret));
            var stream = new SicBlockCipher(new AesEngine());
            stream.Init(true, new ParametersWithIV(new KeyParameter(secrets.AesSecret), new byte[16]));
            var mac = secrets.EgressMac;

            var header = new byte[16];
            header[3] = 0xc2; header[4] = 0x80; header[5] = 0x80;
            var headerCipher = new byte[16];
            stream.ProcessBlock(header, 0, headerCipher, 0);

            var digest = mac.DigestFirst16();
            var seed = new byte[16];
            aes.ProcessBlock(digest, 0, seed, 0);
            for (var i = 0; i < 16; i++) seed[i] ^= headerCipher[i];
            mac.Update(seed);
            var headerMac = mac.DigestFirst16();

            mac.Update(Array.Empty<byte>());
            digest = mac.DigestFirst16();
            aes.ProcessBlock(digest, 0, seed, 0);
            for (var i = 0; i < 16; i++) seed[i] ^= digest[i];
            mac.Update(seed);
            var frameMac = mac.DigestFirst16();

            return headerCipher.Concat(headerMac).Concat(frameMac).ToArray();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [Trait("Category", "Security")]
        public void Given_BaseProtocolMessageLargerThanTwoKiB_When_FrameIsRead_Then_RejectedBeforeDecoding(int msgId)
        {
            var (secrets, reader) = PairedSecrets();
            var writer = new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac);
            var frame = writer.WriteFrame(msgId, new byte[BaseProtocolMaxMsgSize + 1]);

            Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_BaseProtocolMessageOfExactlyTwoKiB_When_FrameIsRead_Then_Accepted()
        {
            var (secrets, reader) = PairedSecrets();
            var writer = new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac);
            var frame = writer.WriteFrame(2, new byte[BaseProtocolMaxMsgSize]);

            var (msgId, payload) = reader.ReadFrame(frame);

            Assert.Equal(2, msgId);
            Assert.Equal(BaseProtocolMaxMsgSize, payload.Length);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_SubprotocolMessageLargerThanTwoKiB_When_FrameIsRead_Then_StillAccepted()
        {
            var (secrets, reader) = PairedSecrets();
            var writer = new RlpxFrameWriter(secrets.AesSecret, secrets.MacSecret, secrets.EgressMac);
            var frame = writer.WriteFrame(0x10, new byte[BaseProtocolMaxMsgSize * 4]);

            var (_, payload) = reader.ReadFrame(frame);

            Assert.Equal(BaseProtocolMaxMsgSize * 4, payload.Length);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_ZeroSizeFrameWithValidMacs_When_BodyIsRead_Then_RejectedWithCryptographicException()
        {
            var (secrets, reader) = PairedSecrets();
            var frame = ZeroSizeFrame(secrets);

            var frameSize = reader.ReadHeader(frame.Take(32).ToArray());
            Assert.Equal(0, frameSize);

            Assert.Throws<CryptographicException>(() => reader.ReadBody(frameSize, frame.Skip(32).ToArray()));
        }
    }
}
