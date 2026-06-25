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
    /// eth wire spec body-rule enforcement per
    /// https://github.com/ethereum/devp2p/blob/master/caps/eth.md.
    /// - BlockRangeUpdate (0x11) is defined only for eth/69+. Receiving it on
    ///   eth/68 is a ProtocolBreach.
    /// - BlockRangeUpdate with earliest > latest is a ProtocolBreach
    ///   ("the peer should be disconnected").
    /// - NewPooledTransactionHashes (0x08) requires types.Length == sizes.Count
    ///   == hashes.Count (geth handleNewPooledTransactionHashes
    ///   "NewPooledTransactionHashes: invalid len of fields").
    /// - Transactions (0x02) "empty Transactions messages are discouraged and
    ///   may lead to disconnection".
    /// </summary>
    [Collection("RlpxLoopback")]
    public class Eth68SpecBodyViolationTests
    {
        private static DevP2PConfig BuildConfig() => new DevP2PConfig
        {
            ClientId = "Nethereum/eth-spec-body-test",
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

        private static Eth68ServerSession BuildSession(RlpxConnection conn, int remoteProtocolVersion)
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
            var handler = new Eth68DecodeDispatchSplitTests.NullEth68Handler();
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
        public async Task Given_BlockRangeUpdateWithEarliestGreaterThanLatest_When_HandleEthMessageAsync_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 69);

                var payload = BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
                {
                    EarliestBlock = 100,
                    LatestBlock = 50,
                    LatestBlockHash = new byte[32]
                });

                await session.HandleEthMessageAsync(EthMessageIds.BlockRangeUpdate, payload);

                Assert.False(serverConn.IsConnected,
                    "earliest > latest is an explicit ProtocolBreach per devp2p caps/eth.md");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_NewPooledTransactionHashesWithMismatchedLengths_When_HandleEthMessageAsync_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 68);

                var mismatched = new NewPooledTransactionHashesMessage
                {
                    Types = new byte[] { 0, 1 },
                    Sizes = new List<long> { 100 },
                    Hashes = new List<byte[]> { new byte[32], new byte[32], new byte[32] }
                };
                var payload = NewPooledTransactionHashesMessageEncoder.Encode(mismatched);

                await session.HandleEthMessageAsync(EthMessageIds.NewPooledTransactionHashes, payload);

                Assert.False(serverConn.IsConnected,
                    "NewPooledTransactionHashes mismatched lengths must trigger ProtocolBreach (geth parity)");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_EmptyTransactionsMessage_When_HandleEthMessageAsync_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 68);

                var payload = TransactionsMessageEncoder.Encode(new TransactionsMessage
                {
                    Transactions = new List<ISignedTransaction>()
                });

                await session.HandleEthMessageAsync(EthMessageIds.Transactions, payload);

                Assert.False(serverConn.IsConnected,
                    "empty Transactions message is a ProtocolBreach per spec body rule");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_BlockRangeUpdateReceivedOnEth68_When_HandleEthMessageAsync_Then_DisconnectsWithProtocolBreach()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 68);

                var payload = BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
                {
                    EarliestBlock = 0,
                    LatestBlock = 100,
                    LatestBlockHash = new byte[32]
                });

                await session.HandleEthMessageAsync(EthMessageIds.BlockRangeUpdate, payload);

                Assert.False(serverConn.IsConnected,
                    "BlockRangeUpdate (msg-id 0x11) is undefined for eth/68 — ProtocolBreach");
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }

        [Fact]
        public async Task Given_BlockRangeUpdateOnEth69WithValidBody_When_HandleEthMessageAsync_Then_SessionRemainsConnected()
        {
            var (listener, serverConn, clientConn) = await HandshakeOverLoopbackAsync();
            try
            {
                var session = BuildSession(serverConn, remoteProtocolVersion: 69);

                var received = new TaskCompletionSource<BlockRangeUpdateMessage>();
                session.BlockRangeUpdateReceived += (_, msg) => received.TrySetResult(msg);

                var payload = BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
                {
                    EarliestBlock = 0,
                    LatestBlock = 1000,
                    LatestBlockHash = new byte[32]
                });

                await session.HandleEthMessageAsync(EthMessageIds.BlockRangeUpdate, payload);

                Assert.True(serverConn.IsConnected,
                    "well-formed BlockRangeUpdate on eth/69 must NOT trigger disconnect");
                var dispatched = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Equal(0ul, dispatched.EarliestBlock);
                Assert.Equal(1000ul, dispatched.LatestBlock);
            }
            finally
            {
                try { clientConn.Dispose(); } catch { }
                await listener.StopAsync();
            }
        }
    }
}
