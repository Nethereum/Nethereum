using System;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class PeerListenerNewBlockIntegrationTests
    {
        private const ulong NetworkId = 424242;
        private readonly ITestOutputHelper _output;

        public PeerListenerNewBlockIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Given_APeerIsConnectedToTheServeListener_When_AProducedBlockIsBroadcast_Then_ThePeerReceivesIt()
        {
            var genesisHash = GenesisHash();
            var listenerKey = EthECKey.GenerateKey();

            await using var listener = StartListener(listenerKey, genesisHash, out var options);

            var peer = await ConnectPeerAsync(listenerKey, listener.Port, genesisHash);

            await Helpers.Wait.UntilAsync(() => listener.EthPeerCount == 1, TimeSpan.FromSeconds(5));
            Assert.Equal(1, listener.EthPeerCount);

            var header = BuildHeader(blockNumber: 1, parentHash: genesisHash);
            var received = ReadNewBlockAsync(peer);

            await EthPeers.BroadcastAsync(Eth68MessageIds.NewBlock, NewBlockMessageEncoder.Encode(new NewBlockMessage
            {
                Header = header,
                Transactions = new System.Collections.Generic.List<ISignedTransaction>(),
                Uncles = new System.Collections.Generic.List<BlockHeader>(),
                Withdrawals = new System.Collections.Generic.List<Withdrawal>(),
                TotalDifficulty = BigInteger.One
            }));

            var pushed = await received.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(BigInteger.One, (BigInteger)pushed.Header.BlockNumber);
            Assert.Equal(
                RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header).ToHex(true),
                RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(pushed.Header).ToHex(true));
            _output.WriteLine($"peer received block {pushed.Header.BlockNumber}");
        }

        [Fact]
        public async Task Given_AServeListenerWithANewBlockHandler_When_APeerPushesABlock_Then_TheHandlerReceivesIt()
        {
            var genesisHash = GenesisHash();
            var listenerKey = EthECKey.GenerateKey();

            var receivedTcs = new TaskCompletionSource<NewBlockMessage>();
            await using var listener = StartListener(listenerKey, genesisHash, out var options,
                configure: o => o.OnNewBlockReceived = msg => receivedTcs.TrySetResult(msg));

            var peer = await ConnectPeerAsync(listenerKey, listener.Port, genesisHash);
            await Helpers.Wait.UntilAsync(() => listener.EthPeerCount == 1, TimeSpan.FromSeconds(5));

            var header = BuildHeader(blockNumber: 7, parentHash: genesisHash);
            var payload = NewBlockMessageEncoder.Encode(new NewBlockMessage
            {
                Header = header,
                Transactions = new System.Collections.Generic.List<ISignedTransaction>(),
                Uncles = new System.Collections.Generic.List<BlockHeader>(),
                Withdrawals = new System.Collections.Generic.List<Withdrawal>(),
                TotalDifficulty = BigInteger.One
            });

            await peer.Connection.SendMessageAsync(peer.EthOffset + Eth68MessageIds.NewBlock, payload);

            var received = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new BigInteger(7), (BigInteger)received.Header.BlockNumber);
            _output.WriteLine($"listener handler saw block {received.Header.BlockNumber}");
        }

        [Fact]
        public async Task Given_NoPeersAreConnected_When_AProducedBlockIsBroadcast_Then_ItCompletesWithoutSending()
        {
            var genesisHash = GenesisHash();
            var listenerKey = EthECKey.GenerateKey();
            await using var listener = StartListener(listenerKey, genesisHash, out _);

            Assert.Equal(0, listener.EthPeerCount);

            await EthPeers.BroadcastAsync(Eth68MessageIds.NewBlock, NewBlockMessageEncoder.Encode(new NewBlockMessage
            {
                Header = BuildHeader(blockNumber: 1, parentHash: genesisHash),
                Transactions = new System.Collections.Generic.List<ISignedTransaction>(),
                Uncles = new System.Collections.Generic.List<BlockHeader>(),
                Withdrawals = new System.Collections.Generic.List<Withdrawal>(),
                TotalDifficulty = BigInteger.One
            }));
        }

        private readonly Eth68PeerPool EthPeers = new Eth68PeerPool();

        private PeerListener StartListener(
            EthECKey key, byte[] genesisHash, out PeerListenerOptions options,
            Action<PeerListenerOptions> configure = null)
        {
            options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                ServeSnap = false,
                MirrorRemoteStatus = false,
                ClientId = "Nethereum/newblock-test",
                EthPeerRegistry = EthPeers
            };
            configure?.Invoke(options);

            var listener = new PeerListener(
                key,
                InMemoryChainStoreBundle.Open(),
                options,
                statusTemplate: StatusFor(genesisHash),
                logger: new TestOutputLogger(_output));
            listener.StartAsync().GetAwaiter().GetResult();
            return listener;
        }


        [Fact]
        public async Task Given_ANodeWithInboundPeersOnly_When_ATransactionIsPooled_Then_ThosePeersReceiveTheAnnouncement()
        {
            var genesisHash = GenesisHash();
            var listenerKey = EthECKey.GenerateKey();

            await using var listener = StartListener(listenerKey, genesisHash, out _);
            var peer = await ConnectPeerAsync(listenerKey, listener.Port, genesisHash);

            await Helpers.Wait.UntilAsync(() => EthPeers.Count == 1, TimeSpan.FromSeconds(5));

            var received = ReadMessageIdAsync(peer);
            await EthPeers.BroadcastAsync(
                Eth68MessageIds.NewPooledTransactionHashes,
                NewPooledTransactionHashesMessageEncoder.Encode(new NewPooledTransactionHashesMessage
                {
                    Types = new byte[] { 0 },
                    Sizes = new System.Collections.Generic.List<long> { 110 },
                    Hashes = new System.Collections.Generic.List<byte[]> { GenesisHash() }
                }));

            var msgId = await received.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(peer.EthOffset + Eth68MessageIds.NewPooledTransactionHashes, msgId);
        }

        [Fact]
        public async Task Given_AnInboundPeerThatDisconnects_When_TheRegistryIsInspected_Then_TheSessionIsAlreadyGone()
        {
            var genesisHash = GenesisHash();
            var listenerKey = EthECKey.GenerateKey();

            await using var listener = StartListener(listenerKey, genesisHash, out _);
            var peer = await ConnectPeerAsync(listenerKey, listener.Port, genesisHash);

            await Helpers.Wait.UntilAsync(() => EthPeers.Count == 1, TimeSpan.FromSeconds(5));

            peer.Connection.Dispose();

            await Helpers.Wait.UntilAsync(() => EthPeers.Count == 0, TimeSpan.FromSeconds(10));
            Assert.Equal(0, EthPeers.Count);
        }

        private static Task<int> ReadMessageIdAsync(ConnectedPeer peer) =>
            Task.Run(async () =>
            {
                var (msgId, _) = await peer.Connection.ReceiveMessageAsync();
                return msgId;
            });

        private static async Task<ConnectedPeer> ConnectPeerAsync(EthECKey listenerKey, int port, byte[] genesisHash)
        {
            var connection = new RlpxConnection(EthECKey.GenerateKey(), new DevP2PConfig
            {
                NetworkId = NetworkId,
                ConnectTimeoutMs = 5000,
                HandshakeTimeoutMs = 5000,
                RequestTimeoutMs = 10000
            });

            await connection.ConnectAsync("127.0.0.1", port, listenerKey.GetPubKeyNoPrefix());
            var ethOffset = connection.GetCapabilityOffset("eth");
            var ethVersion = connection.SharedCapabilities.Find(c => c.Name == "eth").Version;

            await connection.SendMessageAsync(
                ethOffset + Eth68MessageIds.Status,
                EncodeStatusFor(ethVersion, genesisHash));
            await connection.ReceiveMessageAsync();

            return new ConnectedPeer(connection, ethOffset);
        }

        private static async Task<NewBlockMessage> ReadNewBlockAsync(ConnectedPeer peer)
        {
            while (true)
            {
                var (msgId, payload) = await peer.Connection.ReceiveMessageAsync();
                if (msgId == peer.EthOffset + Eth68MessageIds.NewBlock)
                    return NewBlockMessageEncoder.Decode(payload);
            }
        }

        private static byte[] EncodeStatusFor(int ethVersion, byte[] genesisHash)
        {
            if (ethVersion >= 69)
                return Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = ethVersion,
                    NetworkId = NetworkId,
                    GenesisHash = genesisHash,
                    ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = genesisHash
                });

            return Eth68StatusMessageEncoder.Encode(StatusFor(genesisHash));
        }

        private static Eth68StatusMessage StatusFor(byte[] genesisHash) => new Eth68StatusMessage
        {
            ProtocolVersion = 68,
            NetworkId = NetworkId,
            TotalDifficulty = BigInteger.One,
            BestHash = genesisHash,
            GenesisHash = genesisHash,
            ForkHash = ForkId.ComputeHash(genesisHash, Array.Empty<ulong>()),
            ForkNext = 0
        };

        private static byte[] GenesisHash()
        {
            var hash = new byte[32];
            for (var i = 0; i < 32; i++) hash[i] = (byte)(i + 1);
            return hash;
        }

        private static BlockHeader BuildHeader(long blockNumber, byte[] parentHash) => new BlockHeader
        {
            ParentHash = parentHash,
            UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
            LogsBloom = new byte[256],
            Difficulty = Nethereum.Util.EvmUInt256.One,
            BlockNumber = blockNumber,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 1700000000,
            ExtraData = new byte[0],
            MixHash = new byte[32],
            Nonce = new byte[8]
        };

        private sealed class TestOutputLogger : ILogger<PeerListener>
        {
            private readonly ITestOutputHelper _output;
            public TestOutputLogger(ITestOutputHelper output) { _output = output; }
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                try { _output.WriteLine($"[{logLevel}] {formatter(state, exception)} {exception}"); } catch { }
            }
        }

        private sealed class ConnectedPeer
        {
            public ConnectedPeer(RlpxConnection connection, int ethOffset)
            {
                Connection = connection;
                EthOffset = ethOffset;
            }

            public RlpxConnection Connection { get; }
            public int EthOffset { get; }
        }
    }
}
