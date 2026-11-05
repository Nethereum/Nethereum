using System;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Discv4;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class Discv4ListenerLoopSecurityTests
    {
        private static Discv4Listener StartListener()
        {
            var key = EthECKey.GenerateKey();
            var listener = new Discv4Listener(key, new Discv4RoutingTable(key.GetPubKeyNoPrefix()));
            listener.Start(0, IPAddress.Loopback);
            return listener;
        }

        private static async Task<bool> PongArrives(Discv4RawPeer peer, IPEndPoint target)
        {
            await peer.SendPingAsync(target);
            var received = await peer.CollectAsync(TimeSpan.FromSeconds(1));
            return received.Exists(r => r.Packet.Type == Discv4MessageType.Pong);
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PingReceivedHandlerThatThrows_When_NextPingArrives_Then_ReadLoopStillAnswers()
        {
            using var listener = StartListener();
            listener.PingReceived += (_, _) => throw new InvalidOperationException("consumer failure");
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            await peer.SendPingAsync(target);
            await Task.Delay(200);

            Assert.True(await PongArrives(peer, target));
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_ErrorOccurredHandlerThatThrows_When_GarbageDatagramThenPingArrive_Then_ReadLoopStillAnswers()
        {
            using var listener = StartListener();
            listener.ErrorOccurred += (_, _) => throw new InvalidOperationException("logging sink failure");
            using var peer = new Discv4RawPeer();
            var target = new IPEndPoint(IPAddress.Loopback, listener.Port);

            var garbage = new byte[120];
            new Random(7).NextBytes(garbage);
            await peer.SendAsync(garbage, target);
            await Task.Delay(300);

            Assert.True(await PongArrives(peer, target),
                "one garbage datagram plus a throwing ErrorOccurred handler ended the read loop; the listener no longer answers");
        }
    }
}
