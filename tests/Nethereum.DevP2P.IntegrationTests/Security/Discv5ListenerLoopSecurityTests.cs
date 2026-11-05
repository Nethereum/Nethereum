using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv5;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv5ListenerLoopSecurityTests
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

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_TalkHandlerThatThrows_When_TalkRequestArrives_Then_ListenerStillRepliesAndKeepsReading()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            listener.RegisterTalkHandler("boom", (_, _) => throw new InvalidOperationException("handler failure"));
            using var peer = new Discv5RawPeer(new IPEndPoint(IPAddress.Loopback, listener.Port), key);
            Assert.True(await peer.HandshakeAsync(Ping()));
            await peer.CollectMessagesAsync(TimeSpan.FromMilliseconds(500));

            var talk = new Discv5TalkReqMessage { RequestId = new byte[] { 3 }, Protocol = System.Text.Encoding.ASCII.GetBytes("boom"), Request = new byte[] { 1 } };
            await peer.SendMessageAsync(Discv5MessageEncoder.EncodeTalkReq(talk));
            var messages = await peer.CollectMessagesAsync(TimeSpan.FromSeconds(1));

            Assert.Contains(messages, m => Discv5MessageEncoder.Unpack(m).type == Discv5MessageType.TalkResp);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_SessionEstablishedSubscriberThatThrows_When_APeerCompletesAHandshake_Then_ListenerStillAnswersNewPeers()
        {
            var (listener, key) = StartListener();
            await using var _ = listener;
            listener.SessionManager.SessionEstablished += (_, _) => throw new NotSupportedException("subscriber failure");
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);
            using var first = new Discv5RawPeer(target, key);
            await first.HandshakeAsync(Ping());
            await Task.Delay(500);

            using var second = new Discv5RawPeer(target, key);
            var challenge = await second.RequestChallengeAsync();

            Assert.True(challenge != null,
                "an exception from a SessionEstablished subscriber escaped Discv5SessionManager.Process and ended the read loop; the listener no longer answers anyone");
        }
    }
}
