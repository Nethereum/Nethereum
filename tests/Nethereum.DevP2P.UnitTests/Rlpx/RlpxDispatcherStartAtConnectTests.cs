using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Rlpx
{
    [Collection("RlpxLoopback")]
    public class RlpxDispatcherStartAtConnectTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/dispatcher-start-at-connect-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 30_000,
            PingIntervalMs = 60_000
        };

        private static async Task<(RlpxListener listener, RlpxConnection serverConn, RlpxConnection clientConn)>
            HandshakeOverLoopbackAsync()
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

            return (listener, serverConn, clientConn);
        }

        [Fact]
        public async Task Given_StartDispatchingCalledRepeatedlyAtConnect_When_SendRequestAsyncFollows_Then_SingleLoopServesTheRequest()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                clientConn.StartDispatching();
                clientConn.StartDispatching();

                var clientEthOffset = clientConn.GetCapabilityOffset("eth");
                var serverEthOffset = serverConn.GetCapabilityOffset("eth");

                var reqId = clientConn.NextRequestId();
                var pendingRequest = clientConn.SendRequestAsync(
                    clientEthOffset + EthMessageIds.GetBlockHeaders,
                    GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage
                    {
                        RequestId = reqId,
                        StartBlock = 0,
                        Limit = 1,
                        Skip = 0,
                        Reverse = false
                    }),
                    clientEthOffset + EthMessageIds.BlockHeaders,
                    reqId,
                    TimeSpan.FromSeconds(5));

                var (msgId, _) = await serverConn.ReceiveMessageAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(serverEthOffset + EthMessageIds.GetBlockHeaders, msgId);

                await serverConn.SendMessageAsync(
                    serverEthOffset + EthMessageIds.BlockHeaders,
                    BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage
                    {
                        RequestId = reqId,
                        Headers = new System.Collections.Generic.List<Nethereum.Model.BlockHeader>()
                    }));

                var responsePayload = await pendingRequest.WaitAsync(TimeSpan.FromSeconds(5));
                var decoded = BlockHeadersMessageEncoder.Decode(responsePayload);
                Assert.Empty(decoded.Headers);
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                try { serverConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }
    }
}
