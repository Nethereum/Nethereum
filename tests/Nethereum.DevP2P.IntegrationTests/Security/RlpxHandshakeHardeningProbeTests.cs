using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpxHandshakeHardeningProbeTests
    {
        private static DevP2PConfig Config() => new DevP2PConfig
        {
            ClientId = "Nethereum/security",
            HandshakeTimeoutMs = 1500,
            ConnectTimeoutMs = 5000
        };

        private static async Task<string> SendRawAuthAsync(Func<byte[], byte[]> mutate)
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, Config());
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, __) => outcome.TrySetResult("accepted");
            listener.PeerFailed += (_, e) => outcome.TrySetResult("failed:" + e.Phase + ":" + e.Exception.GetType().Name);
            listener.Start(0, IPAddress.Loopback);
            try
            {
                var (auth, _) = RlpxHandshake.CreateAuth(EthECKey.GenerateKey(), serverKey.GetPubKeyNoPrefix());
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                await tcp.GetStream().WriteAsync(mutate(auth));
                var winner = await Task.WhenAny(outcome.Task, Task.Delay(TimeSpan.FromSeconds(6)));
                var result = winner == outcome.Task ? outcome.Task.Result : "timeout";

                using var good = new RlpxConnection(EthECKey.GenerateKey(), Config());
                var accepted = new TaskCompletionSource<bool>();
                listener.PeerAccepted += (_, __) => accepted.TrySetResult(true);
                await good.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
                await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, listener.CountInboundForIp(IPAddress.Loopback));
                return result;
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_AuthWithFlippedMacByte_When_ListenerHandles_Then_RejectedAndListenerKeepsServing()
        {
            var outcome = await SendRawAuthAsync(auth =>
            {
                var copy = (byte[])auth.Clone();
                copy[copy.Length - 1] ^= 0x01;
                return copy;
            });

            Assert.StartsWith("failed:Handshake:CryptographicException", outcome);
        }

        [Fact]
        public async Task Given_AuthWithEphemeralPointOffTheCurve_When_ListenerHandles_Then_RejectedAndListenerKeepsServing()
        {
            var outcome = await SendRawAuthAsync(auth =>
            {
                var copy = (byte[])auth.Clone();
                for (var i = 2 + 33; i < 2 + 65; i++) copy[i] = (byte)(i * 7);
                return copy;
            });

            Assert.StartsWith("failed:Handshake", outcome);
        }

        [Fact]
        public async Task Given_AuthPrefixBelowMinimumSize_When_ListenerHandles_Then_RejectedWithoutReadingBody()
        {
            var outcome = await SendRawAuthAsync(auth => new byte[] { 0x00, 0x10 }.AsSpan().ToArray());

            Assert.StartsWith("failed:Handshake:CryptographicException", outcome);
        }

        [Fact]
        public async Task Given_ConnectionThatNeverSendsAuth_When_HandshakeTimeoutElapses_Then_SlotIsReleased()
        {
            var listener = new RlpxListener(EthECKey.GenerateKey(), Config());
            listener.Start(0, IPAddress.Loopback);
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(IPAddress.Loopback, listener.Port);
                await Task.Delay(300);
                Assert.Equal(1, listener.PendingHandshakeCount);

                await Task.Delay(2500);

                Assert.Equal(0, listener.PendingHandshakeCount);
                Assert.Equal(0, listener.CountInboundForIp(IPAddress.Loopback));
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_AuthenticatedFrameWithBadMac_When_ServerReads_Then_ConnectionIsDisconnected()
        {
            var serverKey = EthECKey.GenerateKey();
            var listener = new RlpxListener(serverKey, Config());
            var accepted = new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            listener.PeerAccepted += (_, c) => accepted.TrySetResult(c);
            listener.Start(0, IPAddress.Loopback);
            try
            {
                using var attacker = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
                await attacker.SendFrameAsync(0, HelloMessageEncoder.Encode(RawRlpxPeer.HelloFor(attacker.Key)));
                await attacker.ReadFrameAsync();
                var serverConn = await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                serverConn.StartDispatching();

                var frame = attacker.Writer.WriteFrame(0x10, new byte[] { 0xc0 });
                frame[frame.Length - 1] ^= 0x01;
                await attacker.Stream.WriteAsync(frame);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && !serverConn.IsDisconnected) await Task.Delay(50);
                Assert.True(serverConn.IsDisconnected);
            }
            finally
            {
                await listener.StopAsync();
            }
        }

        [Fact]
        public void Given_SnappyFrameDeclaringMoreThanThirtyTwoMiB_When_FrameIsRead_Then_RejectedBeforeDecoding()
        {
            var initiator = EthECKey.GenerateKey();
            var recipient = EthECKey.GenerateKey();
            var (auth, state) = RlpxHandshake.CreateAuth(initiator, recipient.GetPubKeyNoPrefix());
            var (ack, _, rs) = RlpxHandshake.HandleAuth(recipient, auth);
            var s = RlpxHandshake.HandleAck(state, ack);
            var writer = new RlpxFrameWriter(s.AesSecret, s.MacSecret, s.EgressMac);
            var reader = new RlpxFrameReader(rs.AesSecret, rs.MacSecret, rs.IngressMac);

            var frame = writer.WriteFrame(0x10, new byte[40 * 1024 * 1024]);

            Assert.Throws<CryptographicException>(() => reader.ReadFrame(frame));
        }
    }
}
