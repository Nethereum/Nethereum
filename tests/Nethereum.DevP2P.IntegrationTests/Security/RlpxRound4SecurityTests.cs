using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpxRound4SecurityTests
    {
        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_AuthenticatedPeerSendingMessageIdsOutsideEveryNegotiatedCapabilityToAPullBasedServerSession_When_TheServerReadsThem_Then_TheConnectionIsDropped()
        {
            var serverKey = EthECKey.GenerateKey();
            var config = new DevP2PConfig { ClientId = "Nethereum/security", HandshakeTimeoutMs = 5000 };
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            using var sessionCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            try
            {
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                var received = new System.Collections.Generic.List<int>();
                var serverLoop = Task.Run(async () =>
                {
                    try
                    {
                        while (serverConn.IsConnected && !sessionCts.IsCancellationRequested)
                        {
                            var (id, _) = await serverConn.ReceiveMessageAsync(TimeSpan.FromSeconds(30), sessionCts.Token);
                            lock (received) received.Add(id);
                        }
                    }
                    catch (Exception)
                    {
                    }
                });

                for (var i = 0; i < 5; i++)
                    await attacker.SendFrameAsync(0x7000 + i, new byte[] { 0xc0 });

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!serverConn.IsDisconnected && DateTime.UtcNow < deadline)
                    await Task.Delay(50);

                Assert.True(serverConn.IsDisconnected,
                    "the pull path handed " + received.Count + " out-of-range frames to the session and stayed connected");
            }
            finally
            {
                sessionCts.Cancel();
                attacker.Dispose();
                await listener.StopAsync();
            }
        }

        [Fact]
        public void Given_TwoSnappyDecodedPayloads_When_TheSecondIsDecoded_Then_TheFirstPayloadArrayIsNotReused()
        {
            var a = IronSnappy.Snappy.Decode(IronSnappy.Snappy.Encode(new byte[] { 0xc2, 1, 2, 3, 3, 3, 3, 3, 3, 3, 3 }));
            var snapshot = (byte[]) a.Clone();
            var b = IronSnappy.Snappy.Decode(IronSnappy.Snappy.Encode(new byte[] { 0xc2, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 }));

            Assert.NotSame(a, b);
            Assert.Equal(snapshot, a);
        }
    }
}
