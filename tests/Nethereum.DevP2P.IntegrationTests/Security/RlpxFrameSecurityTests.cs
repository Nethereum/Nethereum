using System;
using System.Security.Cryptography;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxFrameSecurityTests
    {
        private const int GethMaxUint24 = (1 << 24) - 1;

        private static (RlpxFrameWriter attackerWriter, RlpxFrameReader victimReader) PairedSession()
        {
            var initiator = EthECKey.GenerateKey();
            var recipient = EthECKey.GenerateKey();
            var (auth, state) = RlpxHandshake.CreateAuth(initiator, recipient.GetPubKeyNoPrefix());
            var (ack, _, recipientSecrets) = RlpxHandshake.HandleAuth(recipient, auth);
            var initiatorSecrets = RlpxHandshake.HandleAck(state, ack);
            return (
                new RlpxFrameWriter(initiatorSecrets.AesSecret, initiatorSecrets.MacSecret, initiatorSecrets.EgressMac),
                new RlpxFrameReader(recipientSecrets.AesSecret, recipientSecrets.MacSecret, recipientSecrets.IngressMac));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_SnappyFrameDecompressingAboveSixteenMiB_When_FrameIsRead_Then_RejectedAsGethDoes()
        {
            var (writer, reader) = PairedSession();
            var payload = new byte[GethMaxUint24 + 4 * 1024 * 1024];
            var frame = writer.WriteFrame(0x10, payload);

            Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
        }

    }
}
