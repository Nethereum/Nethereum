using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Crypto;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpxPreHelloLimitSecurityTests
    {
        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PeerSendingAFrameHeaderAboveTheBaseProtocolLimitBeforeItsHello_When_TheServerReadsTheHeader_Then_TheConnectionIsClosedWithoutWaitingForTheBody()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 60000 };
            var listener = new RlpxListener(serverKey, config);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            try
            {
                var frame = attacker.Writer.WriteFrame(0, new byte[100_000]);
                await attacker.Stream.WriteAsync(frame.AsMemory(0, 32));

                var buffer = new byte[4096];
                var closed = false;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    while (true)
                    {
                        var read = await attacker.Stream.ReadAsync(buffer, cts.Token);
                        if (read == 0) { closed = true; break; }
                    }
                }
                catch (IOException) { closed = true; }
                catch (OperationCanceledException) { }

                Assert.True(closed, "server kept waiting for the body of a 100 KB pre-Hello frame; geth caps base-protocol messages at 2 KiB (rlpx.go)");
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }

        [Theory]
        [InlineData(2049)]
        [InlineData(2060)]
        [Trait("Category", "Security")]
        public void Given_HelloBodyLargerThanTwoKiBWithinTheChannelLimit_When_TheFrameReaderExtractsIt_Then_ItIsRejectedAsAnOversizedHello(int bodyLength)
        {
            var initiator = EthECKey.GenerateKey();
            var recipient = EthECKey.GenerateKey();
            var (auth, state) = RlpxHandshake.CreateAuth(initiator, recipient.GetPubKeyNoPrefix());
            var (ack, _, recipientSecrets) = RlpxHandshake.HandleAuth(recipient, auth);
            var initiatorSecrets = RlpxHandshake.HandleAck(state, ack);
            var writer = new RlpxFrameWriter(initiatorSecrets.AesSecret, initiatorSecrets.MacSecret, initiatorSecrets.EgressMac);
            var reader = new RlpxFrameReader(recipientSecrets.AesSecret, recipientSecrets.MacSecret, recipientSecrets.IngressMac);

            var frame = writer.WriteFrame(0, new byte[bodyLength]);

            var ex = Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
            Assert.Contains("Hello message size", ex.Message);
        }
    }
}
