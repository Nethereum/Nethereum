using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpxHelloIdentitySecurityTests
    {
        private static DevP2PConfig Config() => new DevP2PConfig
        {
            ClientId = "Nethereum/security",
            HandshakeTimeoutMs = 5000,
            ConnectTimeoutMs = 5000
        };

        private static async Task<(string outcome, int disconnectReason)> SendHelloAsync(EthECKey attackerKey, HelloMessage hello)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, Config());
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, __) => outcome.TrySetResult("accepted");
            listener.PeerFailed += (_, e) => outcome.TrySetResult("failed:" + e.Phase + ":" + e.Exception.GetType().Name + ":" + e.Exception.Message + ":" + e.Exception.InnerException);
            listener.Start(0, IPAddress.Loopback);
            try
            {
                using var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix(), attackerKey);
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
                var result = await outcome.Task.WaitAsync(TimeSpan.FromSeconds(8));
                var reason = -1;
                if (result.StartsWith("failed"))
                {
                    var (msgId, payload) = await attacker.ReadFrameAsync();
                    if (msgId == P2PMessageIds.Disconnect) reason = payload.Last();
                }
                return (result, reason);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloCarryingAnotherNodesSixtyFourByteId_When_ListenerReadsIt_Then_PeerIsRejectedWithUnexpectedIdentity()
        {
            var attackerKey = EthECKey.GenerateKey();
            var someoneElse = EthECKey.GenerateKey().GetPubKeyNoPrefix();

            var (outcome, reason) = await SendHelloAsync(attackerKey, RawRlpxPeer.HelloFor(attackerKey, nodeId: someoneElse));

            Assert.StartsWith("failed", outcome);
            Assert.Equal((int)DisconnectReason.UnexpectedIdentity, reason);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloWithShortNodeId_When_ListenerReadsIt_Then_PeerIsRejectedWithNullIdentity()
        {
            var attackerKey = EthECKey.GenerateKey();

            var (outcome, reason) = await SendHelloAsync(attackerKey, RawRlpxPeer.HelloFor(attackerKey, nodeId: new byte[] { 1, 2, 3 }));

            Assert.StartsWith("failed", outcome);
            Assert.Equal((int)DisconnectReason.NullIdentity, reason);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloWithClientIdAboveTheBound_When_ListenerReadsIt_Then_PeerIsRejected()
        {
            var attackerKey = EthECKey.GenerateKey();

            var (outcome, _) = await SendHelloAsync(attackerKey, RawRlpxPeer.HelloFor(attackerKey, clientId: new string('a', 300)));

            Assert.StartsWith("failed", outcome);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloWithMoreCapabilitiesThanTheBound_When_ListenerReadsIt_Then_PeerIsRejected()
        {
            var attackerKey = EthECKey.GenerateKey();
            var hello = RawRlpxPeer.HelloFor(attackerKey);
            for (var i = 0; i < 100; i++)
                hello.Capabilities.Add(new P2PCapability { Name = "x", Version = i });

            var (outcome, _) = await SendHelloAsync(attackerKey, hello);

            Assert.StartsWith("failed", outcome);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_WellFormedHelloFromTheAuthenticatedKey_When_ListenerReadsIt_Then_PeerIsAccepted()
        {
            var attackerKey = EthECKey.GenerateKey();

            var (outcome, _) = await SendHelloAsync(attackerKey, RawRlpxPeer.HelloFor(attackerKey));

            Assert.True(outcome == "accepted", outcome);
        }
    }
}
