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
    /// <summary>
    /// Verifies the per-connection bounded back-pressure channel inserted between
    /// RlpxConnection's read loop and PushMessageReceived subscribers
    /// (https://github.com/ethereum/devp2p/blob/master/caps/eth.md). A slow
    /// subscriber must not block the read loop; the channel drops the oldest
    /// pending push when capacity is exceeded.
    /// </summary>
    [Collection("RlpxLoopback")]
    public class RlpxPushBackpressureTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/push-backpressure-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 60_000,
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
        public async Task Given_SlowSubscriber_When_PushArrivesDuringRequest_Then_ReadLoopNotBlocked()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                using var subscriberCanProceed = new ManualResetEventSlim(false);
                int subscriberCallCount = 0;

                clientConn.PushMessageReceived += (_, _) =>
                {
                    Interlocked.Increment(ref subscriberCallCount);
                    subscriberCanProceed.Wait(TimeSpan.FromSeconds(5));
                };

                var serverEthOffset = serverConn.GetCapabilityOffset("eth");
                var clientEthOffset = clientConn.GetCapabilityOffset("eth");

                var requestTask = clientConn.RequestAsync(
                    clientEthOffset + EthMessageIds.GetBlockHeaders,
                    GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage
                    {
                        RequestId = 7,
                        StartBlock = 0,
                        Limit = 1,
                        Skip = 0,
                        Reverse = false
                    }),
                    clientEthOffset + EthMessageIds.BlockHeaders);

                await Task.Delay(150);

                for (int i = 0; i < 5; i++)
                {
                    await serverConn.SendMessageAsync(
                        serverEthOffset + EthMessageIds.NewBlockHashes,
                        NewBlockHashesMessageEncoder.Encode(new NewBlockHashesMessage
                        {
                            Entries = new System.Collections.Generic.List<NewBlockHashesMessage.BlockHashEntry>
                            {
                                new() { Hash = new byte[32], Number = (ulong)i }
                            }
                        }));
                }

                await serverConn.SendMessageAsync(
                    serverEthOffset + EthMessageIds.BlockHeaders,
                    BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage
                    {
                        RequestId = 7,
                        Headers = new System.Collections.Generic.List<Nethereum.Model.BlockHeader>()
                    }));

                var (responseMsgId, _) = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(clientEthOffset + EthMessageIds.BlockHeaders, responseMsgId);

                subscriberCanProceed.Set();

                await Task.Delay(500);
                Assert.True(Volatile.Read(ref subscriberCallCount) >= 1,
                    "subscriber must have been invoked at least once via the back-pressure channel");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                try { serverConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_BurstedPushSequence_When_SubscriberDelays_Then_ChannelAbsorbsAndPumps()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                int processed = 0;
                using var firstSeen = new ManualResetEventSlim(false);
                clientConn.PushMessageReceived += (_, _) =>
                {
                    Interlocked.Increment(ref processed);
                    firstSeen.Set();
                };

                var serverEthOffset = serverConn.GetCapabilityOffset("eth");
                var clientEthOffset = clientConn.GetCapabilityOffset("eth");

                var requestTask = clientConn.RequestAsync(
                    clientEthOffset + EthMessageIds.GetBlockHeaders,
                    GetBlockHeadersMessageEncoder.Encode(new GetBlockHeadersMessage
                    {
                        RequestId = 11,
                        StartBlock = 0,
                        Limit = 1,
                        Skip = 0,
                        Reverse = false
                    }),
                    clientEthOffset + EthMessageIds.BlockHeaders);

                for (int i = 0; i < 32; i++)
                {
                    await serverConn.SendMessageAsync(
                        serverEthOffset + EthMessageIds.NewBlockHashes,
                        NewBlockHashesMessageEncoder.Encode(new NewBlockHashesMessage
                        {
                            Entries = new System.Collections.Generic.List<NewBlockHashesMessage.BlockHashEntry>
                            {
                                new() { Hash = new byte[32], Number = (ulong)i }
                            }
                        }));
                }

                await serverConn.SendMessageAsync(
                    serverEthOffset + EthMessageIds.BlockHeaders,
                    BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage
                    {
                        RequestId = 11,
                        Headers = new System.Collections.Generic.List<Nethereum.Model.BlockHeader>()
                    }));

                var (responseMsgId, _) = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(clientEthOffset + EthMessageIds.BlockHeaders, responseMsgId);

                Assert.True(firstSeen.Wait(TimeSpan.FromSeconds(5)),
                    "background pump must drain at least one push from the channel");

                await Task.Delay(500);

                var observed = Volatile.Read(ref processed);
                Assert.True(observed >= 1, $"channel pump delivered {observed} pushes");
                Assert.True(observed <= 32, "pump should not amplify");
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
