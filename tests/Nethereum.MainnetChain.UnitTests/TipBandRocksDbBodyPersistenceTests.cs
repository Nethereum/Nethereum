using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Signer;
using Nethereum.Util;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class TipBandRocksDbBodyPersistenceTests
    {
        private const int Blocks = 300;

        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Given_APassOverManyBlocksOnTheRocksDbBundle_When_TheBandFills_Then_EveryBlockBelowFilledToHasItsTransactionsStored(bool splitHistory)
        {
            var dir = Path.Combine(Path.GetTempPath(), "tipband-body-" + Guid.NewGuid().ToString("N"));
            try
            {
                var chain = BuildChain();
                await using var bundle = RocksDbChainStoreBundle.Open(dir, null, false, new RocksDbStorageOptions { SplitHistoryStore = splitHistory });
                for (int n = 0; n < Blocks; n++)
                    await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);
                bundle.Metadata.Commit(2, chain.Hashes[2]);
                bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
                {
                    SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                    Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = Blocks - 1, Tail = 0, Next = 0 } },
                });

                var logger = new ListLogger();
                var pool = new OnePeerPool();
                var worker = new ServingWorker(chain);
                var service = new TipBandBodyFollowService(
                    new UnusedScheduler(), pool, bundle, new FixedChainActivations(HardforkName.London), worker, logger);
                var backfiller = new ParallelBlockBackfiller(
                    new UnusedScheduler(), pool, worker, bundle, logger: logger, role: "tipband", activations: new FixedChainActivations(HardforkName.London));
                var cursor = new ExecutionHeadBodyFillCursor(bundle);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await service.FillOnceAsync(backfiller, cursor, cts.Token);

                Assert.True((ulong)(Blocks - 1) == cursor.Get(), string.Join(Environment.NewLine, logger.Lines.TakeLast(40)));
                for (int n = 10; n < Blocks; n += 13)
                    await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);
                var missing = new List<int>();
                for (int n = 3; n < Blocks; n++)
                {
                    var stored = await bundle.Transactions.GetByBlockHashAsync(chain.Hashes[n]);
                    if (stored.Count != chain.ExpectedTxCount[n]) missing.Add(n);
                }
                Assert.Empty(missing);
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private sealed class ListLogger : ILogger
        {
            public List<string> Lines { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (Lines) Lines.Add(logLevel + " " + formatter(state, exception) + (exception == null ? "" : " " + exception.Message));
            }
        }

        private sealed class Chain
        {
            public BlockHeader[] Headers = new BlockHeader[Blocks];
            public byte[][] Hashes = new byte[Blocks][];
            public int[] ExpectedTxCount = new int[Blocks];
            public Dictionary<string, int> NumberByHash = new();
            public Dictionary<int, BlockBody> Bodies = new();
            public Dictionary<int, List<Receipt>> Receipts = new();
        }

        private static Chain BuildChain()
        {
            var keccak = new Sha3Keccack();
            var roots = PatriciaBlockRootsProvider.Instance;
            var c = new Chain();
            byte[] prev = new byte[32];
            for (int n = 0; n < Blocks; n++)
            {
                int count = n < 3 || n % 3 == 0 ? 0 : 1 + n % 4;
                var txs = new List<ISignedTransaction>();
                var rcpts = new List<Receipt>();
                for (int k = 0; k < count; k++)
                {
                    txs.Add(MakeTx((byte)n, (byte)k));
                    rcpts.Add(new Receipt { CumulativeGasUsed = new EvmUInt256((ulong)(21000 * (k + 1))) });
                }
                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prev.Clone(),
                    TransactionsHash = count == 0 ? (byte[])EmptyTrieRoot.Clone() : roots.CalculateTransactionsRoot(txs),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = count == 0 ? (byte[])EmptyTrieRoot.Clone() : roots.CalculateReceiptsRoot(rcpts),
                    StateRoot = new byte[32],
                    Difficulty = new EvmUInt256(1UL),
                    GasLimit = 1,
                    Timestamp = 1,
                    ExtraData = Array.Empty<byte>(),
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    LogsBloom = new byte[256],
                    Coinbase = "0x0000000000000000000000000000000000000000",
                };
                var hash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                c.Headers[n] = header;
                c.Hashes[n] = hash;
                c.ExpectedTxCount[n] = count;
                c.NumberByHash[hash.ToHex()] = n;
                c.Bodies[n] = new BlockBody { Transactions = txs, Uncles = new List<BlockHeader>() };
                c.Receipts[n] = rcpts;
                prev = hash;
            }
            return c;
        }

        private static readonly LegacyTransactionSigner TxSigner = new();

        private static ISignedTransaction MakeTx(byte a, byte b)
        {
            var hex = TxSigner.SignTransaction(
                "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318".HexToByteArray(),
                1, "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC", 1 + b, a * 8 + b, 2_000_000_000, 21_000, "");
            return TransactionFactory.CreateTransaction(hex);
        }

        private sealed class OnePeerPool : IPeerPool
        {
            private readonly IEthPeer _peer = new FakeEthPeer();
            public IReadOnlyCollection<IEthPeer> ActivePeers => new[] { _peer };
            public int TargetPeerCount => 1;
            public bool IsPeerActive(Guid id) => id == _peer.Id;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class FakeEthPeer : IEthPeer
        {
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode => "enode://peer@127.0.0.1:30303";
            public string Host => "127.0.0.1";
            public int EthVersion => 69;
            public ulong PeerLatestBlock => Blocks;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private sealed class ServingWorker : IPeerRequestWorker
        {
            private readonly Chain _chain;
            public ServingWorker(Chain chain) => _chain = chain;

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                var bodies = new List<BlockBody>(hashes.Count);
                foreach (var h in hashes)
                    if (_chain.NumberByHash.TryGetValue(h.ToHex(), out var n)) bodies.Add(_chain.Bodies[n]);
                return Task.FromResult(bodies);
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                var result = new List<List<Receipt>>(hashes.Count);
                foreach (var h in hashes)
                    result.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Receipts[n] : new List<Receipt>());
                return Task.FromResult(result);
            }

            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class UnusedScheduler : IFetchRequestScheduler
        {
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }
    }
}
