using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv5HandshakeSecurityTests
    {
        private static byte[] Ping() => Discv5MessageEncoder.EncodePing(new Discv5PingMessage { RequestId = new byte[] { 1 }, EnrSeq = 1 });

        private static (Discv5Listener Listener, EthECKey Key) StartListener()
        {
            var key = EthECKey.GenerateKey();
            var listener = new Discv5Listener(key);
            listener.Start(IPAddress.Loopback, 0);
            listener.LocalEnrEncoded = Discv5RawPeer.SignEnr(key, IPAddress.Loopback, listener.Port, 1);
            return (listener, key);
        }

        private static bool IsPong(byte[] message) => Discv5MessageEncoder.Unpack(message).type == Discv5MessageType.Pong;

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_ValidHandshake_When_PeerSendsPing_Then_ListenerAnswersWithPong()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);

            Assert.True(await peer.HandshakeAsync(Ping()));
            var messages = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));

            Assert.Contains(messages, IsPong);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_FailedHandshakeAttempt_When_TheSameChallengeIsUsedForAValidHandshake_Then_NoSessionIsEstablished()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);

            var challenge = await peer.RequestChallengeAsync();
            Assert.NotNull(challenge);
            await peer.SendRawAsync(peer.BuildHandshake(challenge, Ping(), idSigner: EthECKey.GenerateKey(), keepKeys: false));
            await Task.Delay(100);
            await peer.SendRawAsync(peer.BuildHandshake(challenge, Ping()));
            var messages = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(messages, IsPong);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HandshakeWhoseIdSignatureHasTheHighSValue_When_ListenerVerifiesIt_Then_NoSessionIsEstablished()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);

            var challenge = await peer.RequestChallengeAsync();
            Assert.NotNull(challenge);
            await peer.SendRawAsync(peer.BuildHandshake(challenge, Ping(), flipS: true));
            var messages = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(messages, IsPong);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_CompletedHandshake_When_TheSameHandshakePacketIsReplayed_Then_ListenerDoesNotAnswerAgain()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);

            var challenge = await peer.RequestChallengeAsync();
            Assert.NotNull(challenge);
            var handshake = peer.BuildHandshake(challenge, Ping());
            await peer.SendRawAsync(handshake);
            var first = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));
            Assert.Single(first.Where(IsPong));

            await peer.SendRawAsync(handshake);
            var replay = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));

            Assert.DoesNotContain(replay, IsPong);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_HandshakePacketWithoutAnyChallenge_When_ListenerReceivesIt_Then_NothingIsAnswered()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);
            var forgedChallenge = new Discv5RawPeer.Challenge { Nonce = new byte[12], ChallengeData = new byte[63] };

            await peer.SendRawAsync(peer.BuildHandshake(forgedChallenge, Ping()));
            var messages = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));

            Assert.Empty(messages);
            Assert.Equal(0, listener.SessionManager.SessionCount);
        }
    }
}
