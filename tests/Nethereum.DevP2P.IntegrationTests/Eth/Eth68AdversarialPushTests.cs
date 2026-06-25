using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.Eth
{
    /// <summary>
    /// End-to-end adversarial peer drives malformed and spec-violating push
    /// messages over a real RLPx loopback session and asserts that
    /// <see cref="Eth68ServerSession"/> reacts per the eth wire spec
    /// (https://github.com/ethereum/devp2p/blob/master/caps/eth.md):
    /// decoder failure / body-rule violation → Disconnect(ProtocolBreach);
    /// honest push → session stays alive.
    /// </summary>
    public class Eth68AdversarialPushTests
    {
        private readonly ITestOutputHelper _output;
        public Eth68AdversarialPushTests(ITestOutputHelper output) { _output = output; }

        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/adversarial-push-test",
            HandshakeTimeoutMs = 30_000,
            ConnectTimeoutMs = 30_000,
            RequestTimeoutMs = 30_000,
            ReadTimeoutMs = 60_000,
            PingIntervalMs = 60_000
        };

        private class NullEth68Handler : IEth68RequestHandler
        {
            public Task<IList<BlockHeader>> GetHeadersAsync(GetBlockHeadersMessage request, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockHeader>>(new List<BlockHeader>());

            public Task<IList<BlockBody>> GetBodiesAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<BlockBody>>(new List<BlockBody>());

            public Task<List<List<Receipt>>> GetReceiptsAsync(byte[][] blockHashes, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<List<Receipt>>());

            public Task<IList<ISignedTransaction>> GetPooledTransactionsAsync(byte[][] txHashes, CancellationToken cancellationToken = default)
                => Task.FromResult<IList<ISignedTransaction>>(new List<ISignedTransaction>());
        }

        private async Task<(RlpxListener listener, RlpxConnection serverConn, RlpxConnection clientConn,
            int serverEthOffset, int clientEthOffset, Eth68ServerSession session, Task sessionLoop, CancellationTokenSource sessionCts)>
            StartEth68LoopbackSessionAsync(int serverProtocolVersion)
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

            var serverEthOffset = serverConn.GetCapabilityOffset("eth");
            var clientEthOffset = clientConn.GetCapabilityOffset("eth");

            byte[] genesis = new byte[32];
            for (int i = 0; i < 32; i++) genesis[i] = (byte)i;

            var serverStatus = new Eth68StatusMessage
            {
                ProtocolVersion = serverProtocolVersion,
                NetworkId = 1234,
                TotalDifficulty = BigInteger.One,
                BestHash = genesis,
                GenesisHash = genesis,
                ForkHash = 0u,
                ForkNext = 0
            };
            var clientStatus = new Eth68StatusMessage
            {
                ProtocolVersion = serverProtocolVersion,
                NetworkId = 1234,
                TotalDifficulty = BigInteger.One,
                BestHash = genesis,
                GenesisHash = genesis,
                ForkHash = 0u,
                ForkNext = 0
            };

            await serverConn.SendMessageAsync(serverEthOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(serverStatus));
            await clientConn.SendMessageAsync(clientEthOffset + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(clientStatus));
            var (clientReceivedId, _) = await clientConn.ReceiveMessageAsync();
            Assert.Equal(clientEthOffset + EthMessageIds.Status, clientReceivedId);

            var session = new Eth68ServerSession(serverConn, new NullEth68Handler(), serverStatus);
            session.BindCapabilityOffset(serverEthOffset);
            typeof(Eth68ServerSession)
                .GetProperty(nameof(Eth68ServerSession.RemoteStatus))!
                .SetValue(session, clientStatus);

            var sessionCts = new CancellationTokenSource();
            var sessionLoop = Task.Run(async () =>
            {
                try { await session.RunAsync(cancellationToken: sessionCts.Token); }
                catch { }
            });

            return (listener, serverConn, clientConn, serverEthOffset, clientEthOffset, session, sessionLoop, sessionCts);
        }

        [Fact]
        public async Task Given_RealPeerSendsTruncatedNewBlock_When_SessionReceives_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn, serverEthOffset, clientEthOffset, session, sessionLoop, sessionCts) =
                await StartEth68LoopbackSessionAsync(serverProtocolVersion: 68);
            try
            {
                var truncated = new byte[] { 0xc1, 0x80 };
                await clientConn.SendMessageAsync(clientEthOffset + EthMessageIds.NewBlock, truncated);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && serverConn.IsConnected)
                    await Task.Delay(50);

                Assert.False(serverConn.IsConnected,
                    "decoder throw on real-peer malformed NewBlock must Disconnect(ProtocolBreach)");
                _output.WriteLine("Adversarial truncated NewBlock → Disconnect(ProtocolBreach) as expected");
            }
            finally
            {
                sessionCts.Cancel();
                try { await sessionLoop; } catch { }
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_RealPeerSendsBlockRangeUpdateOnEth68_When_SessionReceives_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn, serverEthOffset, clientEthOffset, session, sessionLoop, sessionCts) =
                await StartEth68LoopbackSessionAsync(serverProtocolVersion: 68);
            try
            {
                var payload = BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
                {
                    EarliestBlock = 0,
                    LatestBlock = 100,
                    LatestBlockHash = new byte[32]
                });
                await clientConn.SendMessageAsync(clientEthOffset + EthMessageIds.BlockRangeUpdate, payload);

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow < deadline && serverConn.IsConnected)
                    await Task.Delay(50);

                Assert.False(serverConn.IsConnected,
                    "BlockRangeUpdate (0x11) is undefined on eth/68 — ProtocolBreach per spec");
                _output.WriteLine("Adversarial BlockRangeUpdate@eth68 → Disconnect(ProtocolBreach) as expected");
            }
            finally
            {
                sessionCts.Cancel();
                try { await sessionLoop; } catch { }
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_RealPeerSendsHonestNewBlockHashes_When_SessionReceives_Then_StaysAliveAndDispatches()
        {
            var (listener, serverConn, clientConn, serverEthOffset, clientEthOffset, session, sessionLoop, sessionCts) =
                await StartEth68LoopbackSessionAsync(serverProtocolVersion: 68);
            try
            {
                var dispatchedTcs = new TaskCompletionSource<NewBlockHashesMessage>();
                session.NewBlockHashesReceived += (_, msg) => dispatchedTcs.TrySetResult(msg);

                var payload = NewBlockHashesMessageEncoder.Encode(new NewBlockHashesMessage
                {
                    Entries = new List<NewBlockHashesMessage.BlockHashEntry>
                    {
                        new() { Hash = new byte[32], Number = 42 }
                    }
                });
                await clientConn.SendMessageAsync(clientEthOffset + EthMessageIds.NewBlockHashes, payload);

                var dispatched = await dispatchedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Single(dispatched.Entries);
                Assert.Equal(42ul, dispatched.Entries[0].Number);
                Assert.True(serverConn.IsConnected, "honest push must NOT drop the wire");
                _output.WriteLine("Honest NewBlockHashes processed; session alive as expected");
            }
            finally
            {
                sessionCts.Cancel();
                try { await sessionLoop; } catch { }
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }
    }
}
