using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Peering;
using Nethereum.DevP2P.Sync.Publish;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests
{
    public class BlockRangeAnnouncementIntegrationTests
    {
        private const ulong NetworkId = 424242;

        private readonly Eth68PeerPool _pool = new Eth68PeerPool();

        private static byte[] Hash(byte seed) => Enumerable.Repeat(seed, 32).ToArray();

        private static byte[] GenesisHash() => Hash(0x11);

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

        private PeerListener StartListener(EthECKey key, Action<PeerListenerOptions> configure = null)
        {
            var options = new PeerListenerOptions
            {
                ListenPort = 0,
                BindAddress = IPAddress.Loopback,
                ServeSnap = false,
                MirrorRemoteStatus = false,
                ClientId = "Nethereum/block-range-test",
                EthPeerRegistry = _pool
            };
            configure?.Invoke(options);

            var listener = new PeerListener(
                key, InMemoryChainStoreBundle.Open(), options, statusTemplate: StatusFor(GenesisHash()));
            listener.StartAsync().GetAwaiter().GetResult();
            return listener;
        }

        private static string EnodeOf(EthECKey key, int port) =>
            $"enode://{key.GetPubKeyNoPrefix().ToHex()}@127.0.0.1:{port}";

        private static Task<SyncPeerSession> DialAsync(EthECKey listenerKey, int port) =>
            SyncPeerSession.ConnectAsync(
                EnodeOf(listenerKey, port), TimeSpan.FromSeconds(10), default, GenesisHash(), NetworkId);

        private static async Task<(RlpxConnection Connection, int EthOffset, int EthVersion)> RawPeerAsync(
            EthECKey listenerKey, int port)
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
            var genesis = GenesisHash();

            await connection.SendMessageAsync(
                ethOffset + EthMessageIds.Status,
                Eth69StatusMessageEncoder.Encode(new Eth69StatusMessage
                {
                    ProtocolVersion = ethVersion,
                    NetworkId = NetworkId,
                    GenesisHash = genesis,
                    ForkHash = ForkId.ComputeHash(genesis, Array.Empty<ulong>()),
                    ForkNext = 0,
                    EarliestBlock = 0,
                    LatestBlock = 0,
                    LatestBlockHash = genesis
                }));
            await connection.ReceiveMessageAsync();
            return (connection, ethOffset, ethVersion);
        }

        private static byte[] RangeOf(ulong latest, byte[] hash, ulong earliest = 0) =>
            BlockRangeUpdateMessageEncoder.Encode(new BlockRangeUpdateMessage
            {
                EarliestBlock = earliest,
                LatestBlock = latest,
                LatestBlockHash = hash
            });

        private static async Task<bool> DisconnectedWithinAsync(SyncPeerSession session, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (session.Connection.IsDisconnected) return true;
                await Task.Delay(25);
            }
            return session.Connection.IsDisconnected;
        }

        private static async Task<List<BlockRangeUpdateMessage>> DrainRangeUpdatesAsync(
            RlpxConnection connection, int ethOffset, TimeSpan quiet)
        {
            var updates = new List<BlockRangeUpdateMessage>();
            while (true)
            {
                Task<(int, byte[])> read;
                try { read = connection.ReceiveMessageAsync(); }
                catch { return updates; }

                var finished = await Task.WhenAny(read, Task.Delay(quiet));
                if (finished != read) return updates;

                var (msgId, payload) = await read;
                if (msgId == ethOffset + EthMessageIds.BlockRangeUpdate)
                    updates.Add(BlockRangeUpdateMessageEncoder.Decode(payload));
            }
        }

        [Fact]
        public async Task Given_AnEth69PlusPeerOfAnAnnouncer_When_TheHeadMovesByTheAnnounceInterval_Then_ThePeerSessionAdvancesItsPeerHeadAndRaisesHeadAdvanced()
        {
            var key = EthECKey.GenerateKey();
            await using var listener = StartListener(key);
            await using var session = await DialAsync(key, listener.Port);
            Assert.True(session.EthVersion >= 69);
            await Helpers.Wait.UntilAsync(() => _pool.Count == 1, TimeSpan.FromSeconds(5));

            var head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 0, LatestBlockHash = Hash(0x22) };
            await using var announcer = new BlockRangeAnnouncer(_pool, () => Task.FromResult(head), announceInterval: 1);
            await announcer.StartAsync();

            var advanced = new TaskCompletionSource<ulong>();
            session.HeadAdvanced += (_, s) => advanced.TrySetResult(s.PeerLatestBlock);

            head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 5, LatestBlockHash = Hash(0x55) };
            await announcer.AnnounceIfDueAsync();

            Assert.Equal(5UL, await advanced.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(5UL, session.PeerLatestBlock);
            Assert.Equal(Hash(0x55), session.PeerLatestBlockHash);
        }

        [Fact]
        public async Task Given_TheDefaultAnnounceInterval_When_TheHeadMovesByLessThanThirtyTwoBlocks_Then_NoBlockRangeUpdateIsSent()
        {
            var key = EthECKey.GenerateKey();
            await using var listener = StartListener(key);
            var (connection, ethOffset, _) = await RawPeerAsync(key, listener.Port);
            await Helpers.Wait.UntilAsync(() => _pool.Count == 1, TimeSpan.FromSeconds(5));

            var head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 0, LatestBlockHash = Hash(0x01) };
            await using var announcer = new BlockRangeAnnouncer(_pool, () => Task.FromResult(head));
            await announcer.StartAsync();

            head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 31, LatestBlockHash = Hash(0x1F) };
            await announcer.AnnounceIfDueAsync();

            var sent = await DrainRangeUpdatesAsync(connection, ethOffset, TimeSpan.FromMilliseconds(600));
            Assert.Empty(sent);
        }

        [Fact]
        public async Task Given_TheDefaultAnnounceInterval_When_TheHeadMovesByThirtyTwoBlocks_Then_OneBlockRangeUpdateCarryingTheNewHeadIsSent()
        {
            var key = EthECKey.GenerateKey();
            await using var listener = StartListener(key);
            var (connection, ethOffset, _) = await RawPeerAsync(key, listener.Port);
            await Helpers.Wait.UntilAsync(() => _pool.Count == 1, TimeSpan.FromSeconds(5));

            var head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 0, LatestBlockHash = Hash(0x01) };
            await using var announcer = new BlockRangeAnnouncer(_pool, () => Task.FromResult(head));
            await announcer.StartAsync();

            head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 32, LatestBlockHash = Hash(0x20) };
            await announcer.AnnounceIfDueAsync();

            var sent = await DrainRangeUpdatesAsync(connection, ethOffset, TimeSpan.FromMilliseconds(600));
            var update = Assert.Single(sent);
            Assert.Equal(32UL, update.LatestBlock);
            Assert.Equal(Hash(0x20), update.LatestBlockHash);
        }

        [Fact]
        public async Task Given_AnAnnouncedHead_When_TheHeadMovesBack_Then_ABlockRangeUpdateIsSentAtOnce()
        {
            var key = EthECKey.GenerateKey();
            await using var listener = StartListener(key);
            var (connection, ethOffset, _) = await RawPeerAsync(key, listener.Port);
            await Helpers.Wait.UntilAsync(() => _pool.Count == 1, TimeSpan.FromSeconds(5));

            var head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 100, LatestBlockHash = Hash(0x64) };
            await using var announcer = new BlockRangeAnnouncer(_pool, () => Task.FromResult(head));
            await announcer.StartAsync();

            head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 99, LatestBlockHash = Hash(0x63) };
            await announcer.AnnounceIfDueAsync();

            var sent = await DrainRangeUpdatesAsync(connection, ethOffset, TimeSpan.FromMilliseconds(600));
            var update = Assert.Single(sent);
            Assert.Equal(99UL, update.LatestBlock);
        }

        [Fact]
        public async Task Given_NoPeersAreConnected_When_TheHeadMoves_Then_AnnouncingCompletesWithoutSending()
        {
            var head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 0, LatestBlockHash = Hash(0x01) };
            await using var announcer = new BlockRangeAnnouncer(_pool, () => Task.FromResult(head), announceInterval: 1);
            await announcer.StartAsync();

            head = new BlockRangeUpdateMessage { EarliestBlock = 0, LatestBlock = 3, LatestBlockHash = Hash(0x03) };
            await announcer.AnnounceIfDueAsync();

            Assert.Equal(0, _pool.Count);
        }

        [Theory]
        [InlineData("ZeroHash")]
        [InlineData("EarliestAboveLatest")]
        public async Task Given_APeerSessionOnEth69Plus_When_ThePeerSendsAnInvalidBlockRangeUpdate_Then_TheSessionDisconnectsAndKeepsItsPeerHead(string kind)
        {
            var key = EthECKey.GenerateKey();
            await using var listener = StartListener(key);
            await using var session = await DialAsync(key, listener.Port);
            await Helpers.Wait.UntilAsync(() => _pool.Count == 1, TimeSpan.FromSeconds(5));

            var payload = kind == "ZeroHash"
                ? RangeOf(9, new byte[32])
                : RangeOf(5, Hash(0x05), earliest: 6);
            await _pool.BroadcastAsync(EthMessageIds.BlockRangeUpdate, payload);

            Assert.True(await DisconnectedWithinAsync(session, TimeSpan.FromSeconds(5)));
            Assert.Equal(0UL, session.PeerLatestBlock);
        }

        [Fact]
        public async Task Given_APeerSessionOnEth69Plus_When_ThePeerSendsNewBlock_Then_TheSessionDisconnects()
        {
            var key = EthECKey.GenerateKey();
            await using var listener = StartListener(key);
            await using var session = await DialAsync(key, listener.Port);
            Assert.True(session.EthVersion >= 69);
            await Helpers.Wait.UntilAsync(() => _pool.Count == 1, TimeSpan.FromSeconds(5));

            var header = new BlockHeader
            {
                ParentHash = GenesisHash(),
                UnclesHash = "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = new byte[32],
                TransactionsHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                ReceiptHash = "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                LogsBloom = new byte[256],
                Difficulty = Nethereum.Util.EvmUInt256.One,
                BlockNumber = 1,
                GasLimit = 30_000_000,
                Timestamp = 1700000000,
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            await _pool.BroadcastAsync(EthMessageIds.NewBlock, NewBlockMessageEncoder.Encode(new NewBlockMessage
            {
                Header = header,
                Transactions = new List<ISignedTransaction>(),
                Uncles = new List<BlockHeader>(),
                Withdrawals = new List<Withdrawal>(),
                TotalDifficulty = BigInteger.One
            }));

            Assert.True(await DisconnectedWithinAsync(session, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task Given_AServeListenerWithABlockRangeHandler_When_APeerSendsABlockRangeUpdate_Then_TheHandlerReceivesIt()
        {
            var key = EthECKey.GenerateKey();
            var received = new TaskCompletionSource<BlockRangeUpdateMessage>();
            await using var listener = StartListener(key, o => o.OnBlockRangeUpdateReceived = msg => received.TrySetResult(msg));
            var (connection, ethOffset, version) = await RawPeerAsync(key, listener.Port);
            Assert.True(version >= 69);
            await Helpers.Wait.UntilAsync(() => listener.EthPeerCount == 1, TimeSpan.FromSeconds(5));

            await connection.SendMessageAsync(ethOffset + EthMessageIds.BlockRangeUpdate, RangeOf(7, Hash(0x07)));

            var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(7UL, update.LatestBlock);
            Assert.Equal(Hash(0x07), update.LatestBlockHash);
        }
    }
}
