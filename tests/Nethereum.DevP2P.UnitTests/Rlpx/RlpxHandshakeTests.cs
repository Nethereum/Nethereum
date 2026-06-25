using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    public class RlpxHandshakeTests
    {
        [Fact]
        public void AuthAck_RoundTrip_DerivesSameAesKey()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var (authPacket, initiatorState) = RlpxHandshake.CreateAuth(
                keyA, keyB.GetPubKeyNoPrefix());

            var (ackPacket, recipientState, recipientSecrets) = RlpxHandshake.HandleAuth(
                keyB, authPacket);

            var initiatorSecrets = RlpxHandshake.HandleAck(initiatorState, ackPacket);

            Assert.Equal(initiatorSecrets.AesSecret, recipientSecrets.AesSecret);
            Assert.Equal(initiatorSecrets.MacSecret, recipientSecrets.MacSecret);
        }

        [Fact]
        public void AuthPacket_SizePrefix_MatchesBody()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var (authPacket, _) = RlpxHandshake.CreateAuth(keyA, keyB.GetPubKeyNoPrefix());

            var declaredSize = (authPacket[0] << 8) | authPacket[1];
            Assert.Equal(authPacket.Length - 2, declaredSize);
        }

        [Fact]
        public void AuthPacket_WithinMaxHandshakeSize()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var (authPacket, _) = RlpxHandshake.CreateAuth(keyA, keyB.GetPubKeyNoPrefix());

            Assert.True(authPacket.Length <= 2048);
        }

        [Fact]
        public void FullHandshake_ProducesWorkingFrameLayer()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var (authPacket, initiatorState) = RlpxHandshake.CreateAuth(
                keyA, keyB.GetPubKeyNoPrefix());
            var (ackPacket, _, recipientSecrets) = RlpxHandshake.HandleAuth(
                keyB, authPacket);
            var initiatorSecrets = RlpxHandshake.HandleAck(initiatorState, ackPacket);

            var writer = new RlpxFrameWriter(
                initiatorSecrets.AesSecret, initiatorSecrets.MacSecret,
                initiatorSecrets.EgressMac);
            var reader = new RlpxFrameReader(
                recipientSecrets.AesSecret, recipientSecrets.MacSecret,
                recipientSecrets.IngressMac);

            var payload = new byte[] { 0xc3, 0x01, 0x02, 0x03 };
            var frame = writer.WriteFrame(0x00, payload);
            var (msgId, decoded) = reader.ReadFrame(frame);

            Assert.Equal(0x00, msgId);
            Assert.Equal(payload, decoded);
        }

        [Fact]
        public void FullHandshake_BidirectionalFrames()
        {
            var keyA = EthECKey.GenerateKey();
            var keyB = EthECKey.GenerateKey();

            var (authPacket, initiatorState) = RlpxHandshake.CreateAuth(
                keyA, keyB.GetPubKeyNoPrefix());
            var (ackPacket, _, recipientSecrets) = RlpxHandshake.HandleAuth(
                keyB, authPacket);
            var initiatorSecrets = RlpxHandshake.HandleAck(initiatorState, ackPacket);

            // A → B
            var writerAB = new RlpxFrameWriter(
                initiatorSecrets.AesSecret, initiatorSecrets.MacSecret,
                initiatorSecrets.EgressMac);
            var readerAB = new RlpxFrameReader(
                recipientSecrets.AesSecret, recipientSecrets.MacSecret,
                recipientSecrets.IngressMac);

            // B → A
            var writerBA = new RlpxFrameWriter(
                recipientSecrets.AesSecret, recipientSecrets.MacSecret,
                recipientSecrets.EgressMac);
            var readerBA = new RlpxFrameReader(
                initiatorSecrets.AesSecret, initiatorSecrets.MacSecret,
                initiatorSecrets.IngressMac);

            // A sends Hello to B
            var helloA = new byte[] { 0xc5, 0x05, 0x80, 0xc0, 0x80, 0x80 };
            var frameAB = writerAB.WriteFrame(0x00, helloA);
            var (msgIdAB, decodedAB) = readerAB.ReadFrame(frameAB);
            Assert.Equal(0x00, msgIdAB);
            Assert.Equal(helloA, decodedAB);

            // B sends Hello to A
            var helloB = new byte[] { 0xc5, 0x05, 0x80, 0xc0, 0x80, 0x80 };
            var frameBA = writerBA.WriteFrame(0x00, helloB);
            var (msgIdBA, decodedBA) = readerBA.ReadFrame(frameBA);
            Assert.Equal(0x00, msgIdBA);
            Assert.Equal(helloB, decodedBA);

            // A sends eth message to B (Snappy compressed)
            var ethMsg = new byte[] { 0xc4, 0x01, 0x02, 0x03, 0x04 };
            var frameEth = writerAB.WriteFrame(0x10, ethMsg);
            var (msgIdEth, decodedEth) = readerAB.ReadFrame(frameEth);
            Assert.Equal(0x10, msgIdEth);
            Assert.Equal(ethMsg, decodedEth);
        }
    }
}
