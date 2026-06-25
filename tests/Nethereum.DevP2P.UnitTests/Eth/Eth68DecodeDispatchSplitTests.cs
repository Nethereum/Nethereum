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

namespace Nethereum.DevP2P.UnitTests.Eth
{
    /// <summary>
    /// Decode-then-dispatch split for Eth68ServerSession push handling:
    /// decoder throw → ProtocolBreach disconnect (geth parity per
    /// https://github.com/ethereum/devp2p/blob/master/caps/eth.md push policy);
    /// subscriber throw → session lives (caller bug must not drop the wire).
    /// OperationCanceledException from a subscriber propagates so the read
    /// loop honours its caller's CancellationToken.
    /// </summary>
    [Collection("RlpxLoopback")]
    public class Eth68DecodeDispatchSplitTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/eth-decode-dispatch-test",
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

        private static Eth68ServerSession BuildSession(RlpxConnection conn, int remoteProtocolVersion = 68)
        {
            var localStatus = new Eth68StatusMessage
            {
                ProtocolVersion = remoteProtocolVersion,
                NetworkId = 1,
                TotalDifficulty = BigInteger.One,
                BestHash = new byte[32],
                GenesisHash = new byte[32],
                ForkHash = 0u,
                ForkNext = 0
            };
            var handler = new NullEth68Handler();
            var session = new Eth68ServerSession(conn, handler, localStatus);
            session.BindCapabilityOffset(0);
            typeof(Eth68ServerSession)
                .GetProperty(nameof(Eth68ServerSession.RemoteStatus))!
                .SetValue(session, new Eth68StatusMessage
                {
                    ProtocolVersion = remoteProtocolVersion,
                    NetworkId = 1,
                    TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32],
                    GenesisHash = new byte[32],
                    ForkHash = 0u,
                    ForkNext = 0
                });
            return session;
        }

        [Fact]
        public async Task Given_DecoderThrowsOnMalformedNewBlock_When_HandleEthMessageAsync_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 68);

                var truncatedPayload = new byte[] { 0xc1, 0x80 };
                await session.HandleEthMessageAsync(EthMessageIds.NewBlock, truncatedPayload);

                Assert.False(serverConn.IsConnected,
                    "decoder throw must trigger Disconnect(ProtocolBreach) — geth parity");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_SubscriberThrows_When_NewBlockReceived_Then_SessionRemainsConnected()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 68);

                session.NewBlockReceived += (_, _) => throw new InvalidOperationException("subscriber bug");

                var validNewBlock = BuildValidNewBlockPayload();
                await session.HandleEthMessageAsync(EthMessageIds.NewBlock, validNewBlock);

                Assert.True(serverConn.IsConnected,
                    "subscriber-thrown exception must NOT tear down the wire connection");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_SubscriberThrowsOperationCanceledException_When_NewBlockReceived_Then_Propagates()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 68);

                session.NewBlockReceived += (_, _) => throw new OperationCanceledException();

                var validNewBlock = BuildValidNewBlockPayload();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    session.HandleEthMessageAsync(EthMessageIds.NewBlock, validNewBlock));
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        internal static byte[] BuildValidNewBlockPayload()
        {
            var msg = new NewBlockMessage
            {
                Header = new BlockHeader
                {
                    ParentHash = new byte[32],
                    UnclesHash = new byte[32],
                    Coinbase = "0x0000000000000000000000000000000000000000",
                    StateRoot = new byte[32],
                    TransactionsHash = new byte[32],
                    ReceiptHash = new byte[32],
                    LogsBloom = new byte[256],
                    Difficulty = Nethereum.Util.EvmUInt256.One,
                    BlockNumber = 1,
                    GasLimit = 30_000_000,
                    GasUsed = 0,
                    Timestamp = 1700000000,
                    ExtraData = new byte[0],
                    MixHash = new byte[32],
                    Nonce = new byte[8]
                },
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
                Withdrawals = null,
                TotalDifficulty = BigInteger.One
            };
            return NewBlockMessageEncoder.Encode(msg);
        }

        internal class NullEth68Handler : IEth68RequestHandler
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
    }
}
