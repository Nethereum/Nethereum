using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Rlpx;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class ParallelBlockBackfillerStallWatchdogTests
    {
        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task Given_the_backfill_cursor_unchanged_past_the_stall_threshold_When_the_watchdog_fires_Then_BackfillStalledException_is_thrown()
        {
            var chain = BuildChain(blockCount: 5);
            using var bundle = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < 5; n++)
                await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);

            var clock = new SteppingClock(DateTimeOffset.UnixEpoch, ParallelBlockBackfiller.HardStallThreshold);
            var backfiller = new ParallelBlockBackfiller(
                new UnusedScheduler(), new EmptyPeerPool(), new UnusedWorker(), bundle,
                utcNow: clock.UtcNow);
            backfiller.SetPersistWaitLogIntervalForTest(TimeSpan.FromMilliseconds(1));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var ex = await Assert.ThrowsAsync<BackfillStalledException>(
                () => backfiller.BackfillAsync(0, 4, headersFromStore: true, cts.Token));

            Assert.Equal(0UL, ex.Cursor);
        }

        [Fact]
        public async Task Given_no_pending_work_When_the_watchdog_ticks_past_the_stall_threshold_Then_it_does_not_fire()
        {
            using var bundle = InMemoryChainStoreBundle.Open();

            var clock = new SteppingClock(DateTimeOffset.UnixEpoch, ParallelBlockBackfiller.HardStallThreshold);
            var backfiller = new ParallelBlockBackfiller(
                new UnusedScheduler(), new EmptyPeerPool(), new UnusedWorker(), bundle,
                utcNow: clock.UtcNow);
            backfiller.SetPersistWaitLogIntervalForTest(TimeSpan.FromMilliseconds(1));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var result = await backfiller.BackfillAsync(0, 4, headersFromStore: true, cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(0UL, result.BlocksWritten);
        }

        [Fact]
        public async Task Given_the_cursor_advancing_When_the_watchdog_ticks_Then_it_does_not_fire()
        {
            var chain = BuildChain(blockCount: 10);
            using var bundle = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < 10; n++)
                await bundle.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);

            var pool = new OnePeerPool();
            var worker = new ServingWorker(chain);
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), pool, worker, bundle);
            backfiller.SetPersistWaitLogIntervalForTest(TimeSpan.FromMilliseconds(25));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await backfiller.BackfillAsync(0, 9, headersFromStore: true, cts.Token);

            Assert.True(result.Ran);
            Assert.Equal(10UL, result.BlocksWritten);
        }

        [Fact]
        public async Task Given_a_stage_task_ignores_cancel_When_draining_stages_Then_it_returns_within_the_drain_timeout_instead_of_hanging()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), new EmptyPeerPool(), new UnusedWorker(), bundle);
            backfiller.SetStageDrainTimeoutForTest(TimeSpan.FromMilliseconds(100));

            var neverCompletes = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await backfiller.DrainStagesOrAbandonAsync(neverCompletes).WaitAsync(cts.Token);
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"drain hung for {sw.Elapsed}");
        }

        [Fact]
        public void Given_a_peer_serves_fewer_blocks_than_requested_When_adapting_receipt_capacity_Then_it_shrinks_toward_served_and_grows_when_full()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), new EmptyPeerPool(), new UnusedWorker(), bundle);

            backfiller.SetReceiptCapacityForTest(96);
            backfiller.AdaptReceiptCapacity(requested: 96, served: 7);
            Assert.Equal(7, backfiller.CurrentReceiptCapacity);

            backfiller.AdaptReceiptCapacity(requested: 7, served: 1);
            Assert.Equal(ParallelBlockBackfiller.MinReceiptCapacityPerPeer, backfiller.CurrentReceiptCapacity);

            backfiller.SetReceiptCapacityForTest(20);
            backfiller.AdaptReceiptCapacity(requested: 20, served: 20);
            Assert.Equal(20 + ParallelBlockBackfiller.ReceiptCapacityGrowStep, backfiller.CurrentReceiptCapacity);

            backfiller.SetReceiptCapacityForTest(50);
            backfiller.AdaptReceiptCapacity(requested: 50, served: 0);
            Assert.Equal(50, backfiller.CurrentReceiptCapacity);
        }

        [Fact]
        public void Given_a_receipt_reservation_older_than_the_ttl_When_reclaiming_Then_the_block_returns_to_pending()
        {
            var chain = BuildChain(3);
            DateTime now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var queue = new BlockTaskQueue(Nethereum.CoreChain.PatriciaBlockRootsProvider.Instance, initialCursor: 0, maxInFlightPerPeer: 1, utcNow: () => now);
            for (int n = 0; n < 3; n++) queue.EnqueueHeader(chain.Headers[n], chain.Hashes[n]);

            var peer = Guid.NewGuid();
            Assert.Equal(3, queue.ReserveReceipts(peer, 3).Count);
            Assert.Equal(0, queue.ReserveReceipts(peer, 3).Count);

            now = now.AddSeconds(120);
            Assert.Equal(0, queue.ReclaimStaleReservations(TimeSpan.FromSeconds(600)));
            Assert.Equal(3, queue.ReclaimStaleReservations(TimeSpan.FromSeconds(90)));

            var reservation2 = queue.ReserveReceipts(peer, 3);
            Assert.Equal(3, reservation2.Count);
        }

        private sealed class SteppingClock
        {
            private readonly TimeSpan _step;
            private DateTimeOffset _current;
            private readonly object _lock = new();

            public SteppingClock(DateTimeOffset start, TimeSpan step)
            {
                _current = start;
                _step = step;
            }

            public DateTimeOffset UtcNow()
            {
                lock (_lock)
                {
                    var value = _current;
                    _current += _step;
                    return value;
                }
            }
        }

        private sealed class Chain
        {
            public BlockHeader[] Headers = Array.Empty<BlockHeader>();
            public byte[][] Hashes = Array.Empty<byte[]>();
            public Dictionary<long, BlockBody> Bodies = new();
            public Dictionary<long, List<Receipt>> Receipts = new();
            public Dictionary<string, long> NumberByHash = new();
        }

        private static Chain BuildChain(int blockCount)
        {
            var keccak = new Sha3Keccack();
            var c = new Chain { Headers = new BlockHeader[blockCount], Hashes = new byte[blockCount][] };

            byte[] prevHash = new byte[32];
            for (long n = 0; n < blockCount; n++)
            {
                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prevHash.Clone(),
                    TransactionsHash = (byte[])EmptyTrieRoot.Clone(),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = (byte[])EmptyTrieRoot.Clone(),
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
                c.Bodies[n] = new BlockBody { Transactions = new List<ISignedTransaction>(), Uncles = new List<BlockHeader>() };
                c.Receipts[n] = new List<Receipt>();
                c.NumberByHash[hash.ToHex()] = n;
                prevHash = hash;
            }
            return c;
        }

        private sealed class EmptyPeerPool : IPeerPool
        {
            public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
            public int TargetPeerCount => 0;
            public bool IsPeerActive(Guid id) => false;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
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
            public ulong PeerLatestBlock => 9;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private sealed class UnusedWorker : IPeerRequestWorker
        {
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
                => throw new NotImplementedException("no peers in the pool — must never be called");
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
                => throw new NotImplementedException("no peers in the pool — must never be called");
            public Task<List<BlockHeader>> GetHeadersAsync(IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }

        private sealed class ServingWorker : IPeerRequestWorker
        {
            private readonly Chain _chain;
            public ServingWorker(Chain chain) => _chain = chain;

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
            {
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
