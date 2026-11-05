using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxConnectionBackpressureSecurityTests
    {
        private static DevP2PConfig Config() => new DevP2PConfig
        {
            ClientId = "Nethereum/security",
            HandshakeTimeoutMs = 5000,
            ConnectTimeoutMs = 5000
        };

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_PeerSendingFramesJustUnderTheCapToASlowConsumer_When_QueuedForPush_Then_TheReadLoopStopsReadingTheSocket()
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, Config());
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var release = new ManualResetEventSlim(false);
            try
            {
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                serverConn.PushMessageReceived += (_, __) =>
                {
                    handlerEntered.TrySetResult(true);
                    release.Wait();
                };
                serverConn.StartDispatching();

                const int frames = 24;
                var payload = new byte[4 * 1024 * 1024];
                new Random(7).NextBytes(payload);
                var sent = 0;
                var sender = Task.Run(async () =>
                {
                    for (var i = 0; i < frames; i++)
                    {
                        await attacker.SendFrameAsync(0x11, payload);
                        Interlocked.Increment(ref sent);
                    }
                });

                await handlerEntered.Task.WaitAsync(TimeSpan.FromSeconds(60));
                var finished = await Task.WhenAny(sender, Task.Delay(TimeSpan.FromSeconds(8))) == sender;

                Assert.False(finished,
                    $"all {frames} frames of 4 MiB were read from the socket while the consumer was stalled; the read loop never applied back-pressure");
                Assert.True(Volatile.Read(ref sent) < frames);
            }
            finally
            {
                release.Set();
                attacker.Dispose();
                await listener.StopAsync();
            }
        }

        [Fact]
        [Trait("Category", "Security")]
        public async Task Given_ConsumerStalledBehindTheByteBudget_When_ItDrains_Then_QueuedMessagesAreDeliveredInOrder()
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, Config());
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var release = new ManualResetEventSlim(false);
            var delivered = new System.Collections.Concurrent.ConcurrentQueue<int>();
            try
            {
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                serverConn.PushMessageReceived += (_, args) =>
                {
                    release.Wait();
                    delivered.Enqueue(args.Payload.Length);
                };
                serverConn.StartDispatching();

                var sizes = new[] { 9_000_000, 9_000_001, 9_000_002, 9_000_003 };
                var sender = Task.Run(async () =>
                {
                    foreach (var size in sizes)
                        await attacker.SendFrameAsync(0x11, new byte[size]);
                });
                await Task.Delay(500);
                release.Set();
                await sender.WaitAsync(TimeSpan.FromSeconds(30));

                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (DateTime.UtcNow < deadline && delivered.Count < sizes.Length)
                    await Task.Delay(50);

                Assert.Equal(sizes, delivered.ToArray());
            }
            finally
            {
                release.Set();
                attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
