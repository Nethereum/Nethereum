using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1aR9BlockPropagationCoverageTests
    {
        private static readonly byte[] Genesis = Enumerable.Repeat((byte)0xC1, 32).ToArray();

        private sealed class Eth68OnlyServer : IAsyncDisposable
        {
            private readonly RlpxListener _listener;
            public EthECKey Key { get; } = EthECKey.GenerateKey();
            public TaskCompletionSource<RlpxConnection> Accepted { get; } =
                new TaskCompletionSource<RlpxConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Eth68OnlyServer()
            {
                _listener = new RlpxListener(Key, new DevP2PConfig { ClientId = "eth68-only", HandshakeTimeoutMs = 5000, MaxEthVersion = 68 });
                _listener.PeerAccepted += (_, c) => Accepted.TrySetResult(c);
                _listener.Start(0, IPAddress.Loopback);
            }

            public string Enode => $"enode://{Key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{_listener.Port}";

            public async Task SendStatusAsync()
            {
                var connection = await Accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await connection.SendMessageAsync(
                    connection.GetCapabilityOffset("eth") + EthMessageIds.Status,
                    Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                    {
                        ProtocolVersion = 68, NetworkId = 7777, TotalDifficulty = BigInteger.One,
                        BestHash = Genesis, GenesisHash = Genesis, ForkHash = 0u, ForkNext = 0
                    }));
            }

            public async Task SendNewBlockAsync(long number)
            {
                var connection = await Accepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await connection.SendMessageAsync(
                    connection.GetCapabilityOffset("eth") + EthMessageIds.NewBlock,
                    NewBlockMessageEncoder.Encode(new NewBlockMessage
                    {
                        Header = new BlockHeader
                        {
                            BlockNumber = number,
                            ParentHash = Enumerable.Repeat((byte)1, 32).ToArray(),
                            UnclesHash = Enumerable.Repeat((byte)2, 32).ToArray(),
                            Coinbase = "0x0000000000000000000000000000000000000000",
                            StateRoot = Enumerable.Repeat((byte)3, 32).ToArray(),
                            TransactionsHash = Enumerable.Repeat((byte)4, 32).ToArray(),
                            ReceiptHash = Enumerable.Repeat((byte)5, 32).ToArray(),
                            LogsBloom = new byte[256],
                            MixHash = new byte[32],
                            ExtraData = new byte[0],
                            Nonce = new byte[8]
                        },
                        Transactions = new List<ISignedTransaction>(),
                        Uncles = new List<BlockHeader>(),
                        TotalDifficulty = BigInteger.One
                    }));
            }

            public async ValueTask DisposeAsync() => await _listener.StopAsync();
        }

        [Theory]
        [InlineData(68, false)]
        [InlineData(69, true)]
        public async Task S1aR9_08_Given_APeerAdmittedAtAnEthVersion_When_TheHeadMovesByTheAnnounceInterval_Then_OnlyEth69PlusPeersReceiveABlockRangeUpdate(int version, bool receives)
        {
            var registry = new Nethereum.DevP2P.Sync.Publish.Eth68PeerPool();
            await using var bundle = Nethereum.CoreChain.Storage.InMemory.InMemoryChainStoreBundle.Open();
            var serverKey = EthECKey.GenerateKey();
            await using var listener = new Nethereum.DevP2P.Sync.Serving.PeerListener(serverKey, bundle, new Nethereum.DevP2P.Sync.Serving.PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                MaxInboundPeers = 4,
                MaxInboundPerIP = 4,
                ServeSnap = false,
                HandshakeTimeoutMs = 5000,
                EthPeerRegistry = registry
            });
            await listener.StartAsync();
            using var peer = await RawRlpxPeer.ConnectAsync(null, listener.Port, serverKey.GetPubKeyNoPrefix());
            var hello = RawRlpxPeer.HelloFor(peer.Key);
            hello.Capabilities[0].Version = version;
            await peer.SendFrameAsync(0, HelloMessageEncoder.Encode(hello));
            await peer.ReadFrameAsync();
            await peer.SendFrameAsync(0x10 + EthMessageIds.Status, version >= 69
                ? Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = version, NetworkId = 1, GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0,
                    EarliestBlock = 0, LatestBlock = 1, LatestBlockHash = Enumerable.Repeat((byte)7, 32).ToArray()
                })
                : Eth68StatusMessageEncoder.Encode(new Eth68StatusMessage
                {
                    ProtocolVersion = version, NetworkId = 1, TotalDifficulty = BigInteger.One,
                    BestHash = new byte[32], GenesisHash = new byte[32], ForkHash = 0u, ForkNext = 0
                }));
            await peer.ReadFrameAsync();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (registry.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
            Assert.Equal(1, registry.Count);

            var head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 0, LatestBlockHash = Enumerable.Repeat((byte)1, 32).ToArray() };
            await using var announcer = new Nethereum.DevP2P.Sync.Serving.BlockRangeAnnouncer(registry, () => Task.FromResult(head), announceInterval: 1);
            await announcer.StartAsync();
            head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 5, LatestBlockHash = Enumerable.Repeat((byte)2, 32).ToArray() };
            await announcer.AnnounceIfDueAsync();

            using var window = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var received = false;
            try
            {
                while (true)
                {
                    var (msgId, _) = await peer.ReadFrameAsync(window.Token);
                    if (msgId == 0x10 + EthMessageIds.BlockRangeUpdate) { received = true; break; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }

            Assert.Equal(receives, received);
        }

        [Fact]
        public async Task S1aR9_07_Given_AnEth68Peer_When_ItSendsAWellFormedNewBlockAheadOfItsHead_Then_OnlyThePeerHeadAdvancesAndTheSessionStaysConnected()
        {
            await using var server = new Eth68OnlyServer();
            var status = server.SendStatusAsync();
            await using var session = await SyncPeerSession.ConnectAsync(
                server.Enode, TimeSpan.FromSeconds(8), CancellationToken.None, Genesis, 7777);
            await status;
            Assert.Equal(68, session.EthVersion);
            var advanced = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
            session.HeadAdvanced += (_, s) => advanced.TrySetResult(s.PeerLatestBlock);

            await server.SendNewBlockAsync(42);

            Assert.Equal(42UL, await advanced.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(42UL, session.PeerLatestBlock);
            Assert.False(session.Connection.IsDisconnected);
        }
    }
}
