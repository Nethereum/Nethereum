using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.EVM;
using Nethereum.Util;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class ReceiptBackfillServiceYieldTests
    {
        private const int TipBlock = 1000;
        private const int ExecutionHead = 100;

        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

        private sealed class NoReceiptScheduler : IFetchRequestScheduler
        {
            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new InvalidOperationException("receipts are fetched by the backfiller receipt role");
            public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false) => throw new NotImplementedException();
            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class CountingWorker : IPeerRequestWorker
        {
            private int _receiptFetches;

            public int ReceiptFetches => Volatile.Read(ref _receiptFetches);

            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            {
                Interlocked.Increment(ref _receiptFetches);
                return Task.FromResult(new List<List<Receipt>>());
            }

            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
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
            public ulong PeerLatestBlock => 1000;
            public uint PeerForkHash => 0;
            public Nethereum.DevP2P.Rlpx.RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + Bound;
            while (DateTime.UtcNow < deadline && !condition())
                await Task.Delay(10);
            return condition();
        }

        [Fact]
        public async Task Given_AnArmedBundleBehindTheTip_When_TheScrubRuns_Then_ItIdles()
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "receipt-yield-" + Guid.NewGuid().ToString("N"));
            try
            {
                await using var bundle = RocksDbChainStoreBundle.Open(dataDir);
                var hashes = new byte[TipBlock + 1][];
                var parent = new byte[32];
                for (int n = 0; n <= TipBlock; n++)
                {
                    var header = new BlockHeader
                    {
                        BlockNumber = new EvmUInt256((ulong)n),
                        ParentHash = parent,
                        UnclesHash = new byte[32],
                        StateRoot = new byte[32],
                        TransactionsHash = new byte[32],
                        ReceiptHash = new byte[32],
                        LogsBloom = new byte[256],
                        MixHash = new byte[32],
                        Nonce = new byte[8],
                        Difficulty = new EvmUInt256(1UL),
                        GasLimit = 1,
                        Timestamp = 1,
                        ExtraData = Array.Empty<byte>(),
                        Coinbase = "0x0000000000000000000000000000000000000000",
                    };
                    hashes[n] = new Sha3Keccack().CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                    await bundle.Blocks.SaveAsync(header, hashes[n]);
                    parent = hashes[n];
                }
                bundle.Metadata.Commit(ExecutionHead, hashes[ExecutionHead]);
                bundle.Metadata.SetLastFetchedBody(TipBlock);
                bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
                {
                    SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                    Subchains = new List<HeaderSubchain> { new HeaderSubchain { Head = TipBlock, Tail = 0, Next = 0 } },
                });
                bundle.SetYieldToExecution(true);

                var worker = new CountingWorker();
                var service = new ReceiptBackfillService(
                    bundle, new NoReceiptScheduler(), new OnePeerPool(),
                    options: new ReceiptBackfillService.Options { IdlePoll = TimeSpan.FromMilliseconds(20) },
                    worker: worker);

                using var cts = new CancellationTokenSource(Bound);
                var run = service.RunAsync(cts.Token);

                await Task.Delay(500);
                Assert.Equal(0, worker.ReceiptFetches);
                Assert.Equal(0, service.Counters.BatchesProcessed);

                bundle.Metadata.Commit(TipBlock - 1, hashes[TipBlock - 1]);
                var resumed = await WaitUntilAsync(() => worker.ReceiptFetches > 0);
                cts.Cancel();
                try { await run.WaitAsync(Bound); }
                catch (OperationCanceledException) { }

                Assert.True(resumed, "the scrub did not process a batch after the follower caught up");
            }
            finally
            {
                try { if (Directory.Exists(dataDir)) Directory.Delete(dataDir, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
