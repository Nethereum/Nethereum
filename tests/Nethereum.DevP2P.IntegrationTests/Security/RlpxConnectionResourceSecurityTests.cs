using System;
using System.IO;
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
    public class RlpxConnectionResourceSecurityTests
    {
        private static DevP2PConfig Config() => new DevP2PConfig
        {
            ClientId = "Nethereum/security",
            HandshakeTimeoutMs = 5000,
            ConnectTimeoutMs = 5000
        };

        private static async Task<(RlpxListener listener, RlpxConnection serverConn, RawRlpxPeer attacker)> ConnectAttackerAsync(
            DevP2PConfig config, int attackerReceiveBuffer = 0)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, config);
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            var attacker = await RawRlpxPeer.ConnectAsync(
                null, listener.Port, serverKey.GetPubKeyNoPrefix(), receiveBufferSize: attackerReceiveBuffer);
            await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
            await attacker.ReadFrameAsync();
            var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return (listener, serverConn, attacker);
        }

        [Fact]
        [Trait("Category", "Security")]
        [Trait("Category", "Performance")]
        public async Task Given_PeerSendingPingsWhileNeverReadingPongs_When_WriteBlocksPastTheFrameWriteTimeout_Then_ConnectionIsDisconnected()
        {
            var (listener, serverConn, attacker) = await ConnectAttackerAsync(Config(), attackerReceiveBuffer: 1024);
            try
            {
                serverConn.StartDispatching();

                var stalled = false;
                for (var round = 0; round < 3000 && !stalled; round++)
                {
                    var chunk = new MemoryStream();
                    for (var i = 0; i < 1000; i++)
                    {
                        var frame = attacker.Writer.WriteFrame(2, new byte[] { 0xc0 });
                        chunk.Write(frame, 0, frame.Length);
                    }
                    using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    try { await attacker.Stream.WriteAsync(chunk.ToArray(), sendCts.Token); }
                    catch (OperationCanceledException) { stalled = true; }
                }
                Assert.True(stalled, "the server never stopped reading; the write-side wedge could not be provoked");

                var deadline = DateTime.UtcNow.AddSeconds(40);
                while (DateTime.UtcNow < deadline && !serverConn.IsDisconnected)
                    await Task.Delay(500);

                Assert.True(serverConn.IsDisconnected,
                    "server connection still open 40s after its Pong writes wedged; geth bounds every frame write with frameWriteTimeout=20s (p2p/server.go:60, p2p/transport.go:93)");
            }
            finally
            {
                attacker.Dispose();
                await listener.StopAsync();
            }
        }
    }
}
