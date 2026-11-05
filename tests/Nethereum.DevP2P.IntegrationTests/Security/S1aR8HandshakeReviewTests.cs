using System;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1aR8HandshakeReviewTests
    {
        private const ulong LocalNetwork = 1337;
        private static readonly byte[] LocalGenesis = Enumerable.Repeat((byte)0xAA, 32).ToArray();

        private static Eth68StatusMessage LocalTemplate() => new Eth68StatusMessage
        {
            ProtocolVersion = 68, NetworkId = LocalNetwork, TotalDifficulty = BigInteger.One,
            BestHash = LocalGenesis, GenesisHash = LocalGenesis, ForkHash = 0u, ForkNext = 0
        };

        private static async Task<bool> WaitDisconnectedAsync(RawRlpxPeer peer, TimeSpan window)
        {
            using var cts = new CancellationTokenSource(window);
            try
            {
                while (true)
                {
                    var (msgId, _) = await peer.ReadFrameAsync(cts.Token);
                    if (msgId == P2PMessageIds.Disconnect) return true;
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        [Theory]
        [InlineData(68, LocalNetwork + 1, true, 68)]
        [InlineData(68, LocalNetwork, false, 68)]
        [InlineData(68, LocalNetwork, true, 67)]
        public async Task S1aR8_01_Given_AListenerWithAConfiguredStatus_When_APeerAnswersWithAnotherNetworkGenesisOrVersion_Then_ItIsNotAdmitted(
            int negotiated, ulong network, bool sameGenesis, int claimedVersion)
        {
            await using var bundle = InMemoryChainStoreBundle.Open();
            var admitted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                MirrorRemoteStatus = false,
                HandshakeTimeoutMs = 5000,
                OnInboundPeerAdded = _ => admitted.TrySetResult(true)
            };
            var serverKey = EthECKey.GenerateKey();
            await using var listener = new PeerListener(serverKey, bundle, options, LocalTemplate());
            await listener.StartAsync();
            using var peer = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(peer.Key);
            hello.Capabilities[0].Version = negotiated;
            await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await peer.ReadFrameAsync();

            await peer.SendFrameAsync(0x10 + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
            {
                ProtocolVersion = claimedVersion, NetworkId = network, TotalDifficulty = BigInteger.One,
                BestHash = LocalGenesis, GenesisHash = sameGenesis ? LocalGenesis : new byte[32], ForkHash = 0u, ForkNext = 0
            }));

            var completed = await Task.WhenAny(admitted.Task, Task.Delay(TimeSpan.FromSeconds(3)));

            Assert.NotSame(admitted.Task, completed);
        }

        private static async Task<RawRlpxPeer> AdmittedPeerAsync(PeerListener listener, EthECKey serverKey, int version)
        {
            var peer = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(peer.Key);
            hello.Capabilities[0].Version = version;
            await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await peer.ReadFrameAsync();
            var status = version >= 69
                ? Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = version, NetworkId = 1, GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0,
                    EarliestBlock = 0, LatestBlock = 1, LatestBlockHash = Enumerable.Repeat((byte)7, 32).ToArray()
                })
                : Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                {
                    ProtocolVersion = version, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                });
            await peer.SendFrameAsync(0x10 + EthMessageIds.Status, status);
            var (msgId, _) = await peer.ReadFrameAsync();
            Assert.Equal(0x10 + EthMessageIds.Status, msgId);
            return peer;
        }

        [Theory]
        [InlineData("SecondStatus")]
        [InlineData("UnsolicitedBlockHeaders")]
        [InlineData("NewBlockHashesOnEth69")]
        [InlineData("NewBlockOnEth69")]
        public async Task S1aR8_05_Given_AnAdmittedPeer_When_ItSendsAMessageTheNegotiatedVersionDoesNotAccept_Then_ItIsDisconnected(string kind)
        {
            await using var bundle = InMemoryChainStoreBundle.Open();
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                HandshakeTimeoutMs = 5000
            };
            var serverKey = EthECKey.GenerateKey();
            await using var listener = new PeerListener(serverKey, bundle, options);
            await listener.StartAsync();
            var version = kind.EndsWith("OnEth69") ? 69 : 68;
            using var peer = await AdmittedPeerAsync(listener, serverKey, version);

            switch (kind)
            {
                case "SecondStatus":
                    await peer.SendFrameAsync(0x10 + EthMessageIds.Status, Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                    {
                        ProtocolVersion = 68, NetworkId = 1, TotalDifficulty = BigInteger.One,
                        BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                    }));
                    break;
                case "UnsolicitedBlockHeaders":
                    await peer.SendFrameAsync(0x10 + EthMessageIds.BlockHeaders,
                        BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage { RequestId = 77 }));
                    break;
                case "NewBlockOnEth69":
                    await peer.SendFrameAsync(0x10 + EthMessageIds.NewBlock, NewBlockMessageEncoder.Encode(new NewBlockMessage
                    {
                        Header = new BlockHeader { BlockNumber = 1, ParentHash = new byte[32] },
                        Transactions = new System.Collections.Generic.List<ISignedTransaction>(),
                        Uncles = new System.Collections.Generic.List<BlockHeader>(),
                        TotalDifficulty = BigInteger.One
                    }));
                    break;
                default:
                    await peer.SendFrameAsync(0x10 + EthMessageIds.NewBlockHashes, NewBlockHashesMessageEncoder.Encode(
                        new NewBlockHashesMessage
                        {
                            Entries = { new NewBlockHashesMessage.BlockHashEntry { Hash = Enumerable.Repeat((byte)9, 32).ToArray(), Number = 5 } }
                        }));
                    break;
            }

            Assert.True(await WaitDisconnectedAsync(peer, TimeSpan.FromSeconds(3)));
        }

        [Fact]
        public async Task S1aR8_09_Given_AnAdmittedPeer_When_ItBroadcastsAnEmptyTransactionsList_Then_ItStaysConnected()
        {
            await using var bundle = InMemoryChainStoreBundle.Open();
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                HandshakeTimeoutMs = 5000
            };
            var serverKey = EthECKey.GenerateKey();
            await using var listener = new PeerListener(serverKey, bundle, options);
            await listener.StartAsync();
            using var peer = await AdmittedPeerAsync(listener, serverKey, 68);

            await peer.SendFrameAsync(0x10 + EthMessageIds.Transactions, new byte[] { 0xc0 });

            Assert.False(await WaitDisconnectedAsync(peer, TimeSpan.FromSeconds(3)));
        }

        private sealed class FakeServer : IAsyncDisposable
        {
            private readonly RlpxListener _listener;
            public EthECKey Key { get; } = EthECKey.GenerateKey();
            public TaskCompletionSource<RlpxConnection> Accepted { get; } =
                new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            public FakeServer()
            {
                _listener = new RlpxListener(Key, new DevP2PConfig { ClientId = "fake", HandshakeTimeoutMs = 5000 });
                _listener.PeerAccepted += (_, c) => Accepted.TrySetResult(c);
                _listener.Start(0, IPAddress.Loopback);
            }

            public string Enode => $"enode://{Key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{_listener.Port}";

            public async Task SendStatusAsync(Eth69StatusMessage status)
            {
                var connection = await Accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await connection.SendMessageAsync(
                    connection.GetCapabilityOffset("eth") + EthMessageIds.Status, Eth69StatusMessageEncoder.Encode(status));
            }

            public async ValueTask DisposeAsync() => await _listener.StopAsync();
        }

        private static readonly byte[] Genesis = Enumerable.Repeat((byte)0xC0, 32).ToArray();

        private static Eth69StatusMessage ServerStatus(int version, ulong earliest, ulong latest, byte[] latestHash) =>
            new Eth69StatusMessage
            {
                ProtocolVersion = version, NetworkId = 7777, GenesisHash = Genesis, ForkHash = 0u, ForkNext = 0,
                EarliestBlock = earliest, LatestBlock = latest, LatestBlockHash = latestHash
            };

        private static async Task<Exception> DialAsync(FakeServer server, Eth69StatusMessage status)
        {
            var send = server.SendStatusAsync(status);
            try
            {
                await using var session = await SyncPeerSession.ConnectAsync(
                    server.Enode, TimeSpan.FromSeconds(8), CancellationToken.None, Genesis, 7777);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
            finally
            {
                try { await send; } catch { }
            }
        }

        [Fact]
        public async Task S1aR8_02_Given_APeerWhoseStatusVersionDiffersFromTheNegotiatedOne_When_TheClientHandshakes_Then_ItIsRefused()
        {
            await using var server = new FakeServer();

            var failure = await DialAsync(server, ServerStatus(70, 0, 5, Enumerable.Repeat((byte)7, 32).ToArray()));

            Assert.NotNull(failure);
        }

        [Fact]
        public async Task S1aR8_03_Given_APeerWhoseStatusAnnouncesAZeroLatestHash_When_TheClientHandshakes_Then_ItIsRefused()
        {
            await using var server = new FakeServer();

            var failure = await DialAsync(server, ServerStatus(71, 0, 5, new byte[32]));

            Assert.NotNull(failure);
        }

        [Fact]
        public async Task S1aR8_03_Given_APeerWhoseStatusAnnouncesEarliestAboveLatest_When_TheClientHandshakes_Then_ItIsRefused()
        {
            await using var server = new FakeServer();

            var failure = await DialAsync(server, ServerStatus(71, 9, 5, Enumerable.Repeat((byte)7, 32).ToArray()));

            Assert.NotNull(failure);
        }

        [Fact]
        public async Task S1aR8_03_Control_Given_AValidStatus_When_TheClientHandshakes_Then_ItIsAccepted()
        {
            await using var server = new FakeServer();

            var failure = await DialAsync(server, ServerStatus(71, 0, 5, Enumerable.Repeat((byte)7, 32).ToArray()));

            Assert.Null(failure);
        }
    }
}
