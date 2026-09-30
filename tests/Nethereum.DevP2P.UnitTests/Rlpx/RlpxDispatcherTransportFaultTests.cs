using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Common;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    [Collection("RlpxLoopback")]
    public class RlpxDispatcherTransportFaultTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/dispatcher-fault-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 60_000,
            PingIntervalMs = 60_000
        };

        [Fact]
        public async Task Given_DispatchedConnection_When_TransportDiesWithoutDisconnectFrame_Then_DisconnectedFiresExactlyOnce()
        {
            var serverKey = EthECKey.GenerateKey();
            var clientKey = EthECKey.GenerateKey();
            var config = BuildConfig();

            var listener = new RlpxListener(serverKey, config);
            var acceptedTcs = new TaskCompletionSource<RlpxConnection>();
            listener.PeerAccepted += (_, conn) => acceptedTcs.TrySetResult(conn);
            listener.Start(port: 0, bindAddress: IPAddress.Loopback);

            var clientConn = new RlpxConnection(clientKey, config);
            await clientConn.ConnectAsync("127.0.0.1", listener.Port, serverKey.GetPubKeyNoPrefix());
            var serverConn = await acceptedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            try
            {
                int disconnectedCount = 0;
                clientConn.Disconnected += (_, __) => Interlocked.Increment(ref disconnectedCount);

                var requestId = clientConn.NextRequestId();
                var pendingRequest = clientConn.SendRequestAsync(
                    sendMsgId: 0x10, payload: Array.Empty<byte>(),
                    expectedResponseMsgId: 0x11, requestId: requestId,
                    timeout: TimeSpan.FromSeconds(30));

                await Task.Delay(300);

                serverConn.Dispose();

                await Task.Delay(TimeSpan.FromSeconds(2));

                Assert.False(clientConn.IsConnected);
                Assert.Equal(1, disconnectedCount);

                await Assert.ThrowsAnyAsync<Exception>(async () => await pendingRequest);
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                try { await listener.StopAsync(); } catch { }
            }
        }
    }
}
