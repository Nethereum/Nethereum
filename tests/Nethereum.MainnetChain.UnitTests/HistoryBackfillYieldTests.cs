using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class HistoryBackfillYieldTests
    {
        private const int TipBlock = 1000;
        private const int ExecutionHead = 100;

        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcdc1b6e8b7a0e8b3e1b6c8b0e8c7a0e8b3e1b6c8b0e8c7a0e8b3e1".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task Given_AFollowerFarBehindTheTip_When_TheTipBandBackfillRuns_Then_ItFetchesAtOnce()
            => await AssertTipBandFetchesAtOnceAsync(armed: false);

        [Fact]
        public async Task Given_AnArmedBundleWhoseFollowerIsFarBehind_When_TheTipBandBackfillRuns_Then_ItFetchesAtOnce()
            => await AssertTipBandFetchesAtOnceAsync(armed: true);

        [Fact]
        public async Task Given_AnArmedBundleWhoseFollowerIsFarBehind_When_TheHistoryBackfillRuns_Then_ItFetchesNothingUntilCaughtUp()
        {
            await WithSeededBundleAsync(async (bundle, chain) =>
            {
                bundle.SetYieldToExecution(true);
                var worker = new CountingServingWorker(chain);
                var log = new CapturingLogger();
                var backfiller = new ParallelBlockBackfiller(
                    new UnusedScheduler(), new OnePeerPool(), worker, bundle, logger: log,
                    activations: new FixedChainActivations(HardforkName.London), role: "history");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var run = backfiller.BackfillAsync(1, TipBlock, headersFromStore: true, cts.Token);

                var idled = await WaitUntilAsync(() => log.Contains("body fetch idle"), TimeSpan.FromSeconds(30));
                Assert.True(idled, "the history backfill never logged the body-fetch idle line");
                Assert.True(log.Contains("yielding to block execution"), "the idle line does not carry the yield text");
                Assert.Equal(0, worker.BodyRequests);

                bundle.Metadata.Commit(TipBlock - 1, chain.Hashes[TipBlock - 1]);
                var fetched = await WaitUntilAsync(() => worker.BodyRequests > 0 || run.IsCompleted, TimeSpan.FromSeconds(30));
                var requested = worker.BodyRequests;
                cts.Cancel();
                try { await run.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch (OperationCanceledException) { }

                Assert.True(fetched && requested > 0, "the history backfill fetched no bodies after the follower caught up");
            });
        }

        private static async Task AssertTipBandFetchesAtOnceAsync(bool armed)
        {
            await WithSeededBundleAsync(async (bundle, chain) =>
            {
                bundle.SetYieldToExecution(armed);
                var pool = new OnePeerPool();
                var worker = new CountingServingWorker(chain);
                var backfiller = new ParallelBlockBackfiller(
                    new UnusedScheduler(), pool, worker, bundle,
                    activations: new FixedChainActivations(HardforkName.London), role: "tipband");
                var cursor = new ExecutionHeadBodyFillCursor(bundle);
                var service = new TipBandBodyFollowService(
                    new UnusedScheduler(), pool, bundle, new FixedChainActivations(HardforkName.London));

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var fill = service.FillOnceAsync(backfiller, cursor, cts.Token);

                await WaitUntilAsync(() => worker.BodyRequests > 0 || fill.IsCompleted, TimeSpan.FromSeconds(30));
                var requested = worker.BodyRequests;
                cts.Cancel();
                try { await fill.WaitAsync(TimeSpan.FromSeconds(30)); }
                catch (OperationCanceledException) { }

                Assert.True(requested > 0, "the tip-band backfill fetched no bodies while the follower was far behind the tip");
            });
        }

        private static async Task WithSeededBundleAsync(Func<RocksDbChainStoreBundle, Chain, Task> body)
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "yield-" + Guid.NewGuid().ToString("N"));
            try
            {
                var chain = BuildChain(TipBlock + 1, firstTxBlock: ExecutionHead + 1, txBlockCount: 10);
                await using var bundle = RocksDbChainStoreBundle.Open(dataDir);
                for (int n = 0; n <= TipBlock; n++)
                    await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);
                bundle.Metadata.Commit(ExecutionHead, chain.Hashes[ExecutionHead]);
                bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
                {
                    SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                    Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = TipBlock, Tail = 0, Next = 0 } },
                });
                await body(bundle, chain);
            }
            finally
            {
                try { if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !condition())
                await Task.Delay(10);
            return condition();
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _lines.Enqueue(formatter(state, exception));

            public bool Contains(string fragment) => _lines.ToArray().Any(l => l.Contains(fragment, StringComparison.Ordinal));
        }


        private sealed class Chain
        {
            public BlockHeader[] Headers = Array.Empty<BlockHeader>();
            public byte[][] Hashes = Array.Empty<byte[]>();
            public Dictionary<long, BlockBody> Bodies = new();
            public Dictionary<long, List<Receipt>> Receipts = new();
            public Dictionary<string, long> NumberByHash = new();
        }

        private static Chain BuildChain(int blockCount, int firstTxBlock, int txBlockCount)
        {
            var keccak = new Sha3Keccack();
            var roots = PatriciaBlockRootsProvider.Instance;
            var c = new Chain
            {
                Headers = new BlockHeader[blockCount],
                Hashes = new byte[blockCount][],
            };

            byte[] prevHash = new byte[32];
            for (long n = 0; n < blockCount; n++)
            {
                var txs = new List<ISignedTransaction>();
                var rcpts = new List<Receipt>();
                if (n >= firstTxBlock && n < firstTxBlock + txBlockCount)
                {
                    txs.Add(MakeTx((byte)n));
                    rcpts.Add(new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) });
                }

                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prevHash.Clone(),
                    TransactionsHash = txs.Count == 0 ? (byte[])EmptyTrieRoot.Clone() : roots.CalculateTransactionsRoot(txs),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = rcpts.Count == 0 ? (byte[])EmptyTrieRoot.Clone() : roots.CalculateReceiptsRoot(rcpts),
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
                c.Bodies[n] = new BlockBody { Transactions = txs, Uncles = new List<BlockHeader>() };
                c.Receipts[n] = rcpts;
                c.NumberByHash[hash.ToHex()] = n;
                prevHash = hash;
            }
            return c;
        }

        private static ISignedTransaction MakeTx(byte seed)
        {
            var receiver = new byte[20];
            receiver[0] = seed;
            return new LegacyTransaction(
                nonce: new byte[] { 0x01 },
                gasPrice: new byte[] { 0x01 },
                gasLimit: new byte[] { 0x52, 0x08 },
                receiveAddress: receiver,
                value: new byte[] { },
                data: new byte[0]);
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
            public ulong PeerLatestBlock => TipBlock;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private sealed class CountingServingWorker : IPeerRequestWorker
        {
            private readonly Chain _chain;
            private int _bodyRequests;

            public CountingServingWorker(Chain chain) => _chain = chain;

            public int BodyRequests => Volatile.Read(ref _bodyRequests);

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                Interlocked.Increment(ref _bodyRequests);
                var bodies = new List<BlockBody>(hashes.Count);
                foreach (var h in hashes)
                    bodies.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Bodies[n] : new BlockBody());
                return Task.FromResult(bodies);
            }

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
                var rcpts = new List<List<Receipt>>(hashes.Count);
                foreach (var h in hashes)
                    rcpts.Add(_chain.NumberByHash.TryGetValue(h.ToHex(), out var n) ? _chain.Receipts[n] : new List<Receipt>());
                return Task.FromResult(rcpts);
            }

            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
                => throw new NotImplementedException("headers come from the store in this mode");
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
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
