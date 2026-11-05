using System;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpxHelloBoundsSecurityTests
    {
        private static DevP2PConfig Config() => new DevP2PConfig
        {
            ClientId = "Nethereum/security",
            HandshakeTimeoutMs = 5000,
            ConnectTimeoutMs = 5000
        };

        private static async Task<string> SendHelloAndObserveAsync(HelloMessage hello)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, Config());
            var accepted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, __) => accepted.TrySetResult("accepted");
            listener.PeerFailed += (_, e) => accepted.TrySetResult("failed:" + e.Phase);
            listener.Start(0, IPAddress.Loopback);
            try
            {
                using var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
                var winner = await Task.WhenAny(accepted.Task, Task.Delay(TimeSpan.FromSeconds(8)));
                return winner == accepted.Task ? accepted.Task.Result : "timeout";
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloLargerThanBaseProtocolMaxMsgSize_When_ListenerReadsIt_Then_PeerIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var hello = RawRlpxPeer.HelloFor(key, clientId: new string('a', 200_000));

            var outcome = await SendHelloAndObserveAsync(hello);

            Assert.StartsWith("failed", outcome);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloWithManyCapabilities_When_ListenerReadsIt_Then_PeerIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var hello = RawRlpxPeer.HelloFor(key);
            for (var i = 0; i < 100_000; i++)
                hello.Capabilities.Add(new P2PCapability { Name = "x" + (i % 10), Version = i % 100 });

            var outcome = await SendHelloAndObserveAsync(hello);

            Assert.StartsWith("failed", outcome);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HelloWithNodeIdNotSixtyFourBytes_When_ListenerReadsIt_Then_PeerIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var hello = RawRlpxPeer.HelloFor(key, nodeId: new byte[] { 1, 2, 3 });

            var outcome = await SendHelloAndObserveAsync(hello);

            Assert.StartsWith("failed", outcome);
        }
    }
}
