using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BackwardBlockWalkerTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        [Fact]
        public async Task HappyPath_WalksFromTipToTarget_AndPersistsHeaders()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 1001);
            var scheduler = new ReverseScheduler(chain);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 192 },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 1000;
            ulong toBlock = 500;
            var fromHash = chain.HashAt(fromBlock);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlock,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(WalkerExitReason.ReachedTarget, result.ExitReason);
            Assert.False(result.MetExistingStore);
            Assert.True(result.HeadersWritten >= 501);

            // LastFetchedHeader should be the LOWEST block (descending cursor)
            Assert.True(bundle.Metadata.GetLastFetchedHeader() <= toBlock);

            // Persisted headers cover the range
            for (ulong n = result.SkeletonBottomBlock; n <= fromBlock; n++)
            {
                var stored = await bundle.Blocks.GetByNumberAsync(new BigInteger(n));
                Assert.NotNull(stored);
                Assert.Equal(n, (ulong)stored.BlockNumber.ToBigInteger());
            }
        }

        [Fact]
        public async Task HeadersOnly_LaysSkeleton_WithoutFetchingBodies()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 1001);
            var scheduler = new ReverseScheduler(chain);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 192, HeadersOnly = true },
                NullLogger<BackwardBlockWalker>.Instance);

            var result = await walker.WalkAsync(
                1000, chain.HashAt(1000), 500,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(WalkerExitReason.ReachedTarget, result.ExitReason);
            Assert.True(result.HeadersWritten >= 501);   // skeleton laid down
            Assert.Equal(0UL, result.BodiesWritten);       // bodies are the filler's job, not the skeleton's
            Assert.True(bundle.Metadata.GetLastFetchedHeader() <= 500);

            // Headers are persisted and available for a concurrent backfiller to consume.
            var stored = await bundle.Blocks.GetByNumberAsync(new BigInteger(750));
            Assert.NotNull(stored);
        }

        [Fact]
        public async Task MetExistingStore_PersistsBatchIdempotently_AndExits()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 500);
            var scheduler = new ReverseScheduler(chain);

            ulong fromBlock = 499;
            var fromHash = chain.HashAt(fromBlock);

            // Pretend block 300 is already in storage with the SAME hash on this chain.
            ulong sharedBlock = 300;
            var sharedHash = chain.HashAt(sharedBlock);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 100 },
                NullLogger<BackwardBlockWalker>.Instance);

            int lookupCalls = 0;
            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) =>
                {
                    lookupCalls++;
                    if (n == sharedBlock)
                        return Task.FromResult<(byte[]? hash, bool exists)>((sharedHash, true));
                    return Task.FromResult<(byte[]? hash, bool exists)>((null, false));
                },
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(WalkerExitReason.MetExistingStore, result.ExitReason);
            Assert.True(result.MetExistingStore);
            Assert.Null(result.DivergenceBlock);

            // The batch containing sharedBlock as its bottom was persisted (idempotent overlap).
            var stored = await bundle.Blocks.GetByNumberAsync(new BigInteger(sharedBlock));
            Assert.NotNull(stored);
            Assert.True(lookupCalls >= 1);
        }

        [Fact]
        public async Task LastKnownGoodDivergence_DoesNotPersistDivergentBatch()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 500);
            var scheduler = new ReverseScheduler(chain);

            ulong fromBlock = 499;
            var fromHash = chain.HashAt(fromBlock);

            ulong divergentBlock = 300;
            var differentHash = new byte[32];
            for (int i = 0; i < 32; i++) differentHash[i] = 0xDE;

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 100 },
                NullLogger<BackwardBlockWalker>.Instance);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) =>
                {
                    if (n == divergentBlock)
                        return Task.FromResult<(byte[]? hash, bool exists)>((differentHash, true));
                    return Task.FromResult<(byte[]? hash, bool exists)>((null, false));
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(WalkerExitReason.LastKnownGoodDivergence, result.ExitReason);
            Assert.Equal(divergentBlock, result.DivergenceBlock);

            // The divergent batch was NOT persisted.
            var stored = await bundle.Blocks.GetByNumberAsync(new BigInteger(divergentBlock));
            Assert.Null(stored);
        }

        [Fact]
        public async Task StructuralGenesis_ExitsAtBlockZero()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 10);
            var scheduler = new ReverseScheduler(chain);

            ulong fromBlock = 9;
            var fromHash = chain.HashAt(fromBlock);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 4 },
                NullLogger<BackwardBlockWalker>.Instance);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(WalkerExitReason.StructuralGenesis, result.ExitReason);
            Assert.Equal(0UL, result.SkeletonBottomBlock);

            var genesis = await bundle.Blocks.GetByNumberAsync(BigInteger.Zero);
            Assert.NotNull(genesis);
        }

        [Fact]
        public async Task AnchorMismatchRetry_SucceedsOnSecondAttempt()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 50);
            var scheduler = new ReverseScheduler(chain) { PoisonFirstHeaderBatch = true };

            ulong fromBlock = 49;
            var fromHash = chain.HashAt(fromBlock);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 50,
                    MaxAnchorRetries = 5,
                    PeerRetryDelay = TimeSpan.Zero,
                },
                NullLogger<BackwardBlockWalker>.Instance);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(scheduler.HeaderCalls >= 2);
        }

        [Fact]
        public async Task PeerPoolEmpty_ReportedWhenRetriesExhausted()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 50);
            var scheduler = new ReverseScheduler(chain) { AlwaysFailHeaders = true };

            ulong fromBlock = 49;
            var fromHash = chain.HashAt(fromBlock);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 16,
                    MaxAnchorRetries = 3,
                    PeerRetryDelay = TimeSpan.Zero,
                },
                NullLogger<BackwardBlockWalker>.Instance);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(WalkerExitReason.PeerPoolEmpty, result.ExitReason);
            Assert.Equal(0UL, result.HeadersWritten);
        }

        [Fact]
        public async Task Cancellation_RaisesOperationCanceledException()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 10);
            var scheduler = new ReverseScheduler(chain) { AlwaysFailHeaders = true };

            ulong fromBlock = 9;
            var fromHash = chain.HashAt(fromBlock);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 4,
                    MaxAnchorRetries = 1000,
                    PeerRetryDelay = TimeSpan.FromMilliseconds(50),
                },
                NullLogger<BackwardBlockWalker>.Instance);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                walker.WalkAsync(
                    fromBlock, fromHash, toBlockNumber: 0,
                    lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                    cts.Token));
        }

        [Fact]
        public async Task BodyCursor_AdvancesIndependentOfHeaderCursor_WhenBodyFetchFails()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 50);
            var scheduler = new ReverseScheduler(chain) { FailBodies = true };

            ulong fromBlock = 49;
            var fromHash = chain.HashAt(fromBlock);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 50 },
                NullLogger<BackwardBlockWalker>.Instance);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.HeadersWritten >= 50);
            // Headers advanced...
            Assert.True(bundle.Metadata.GetLastFetchedHeader() == 0
                        || bundle.Metadata.GetLastFetchedHeader() < fromBlock);
            // ...but bodies did not.
            Assert.Equal(0UL, result.BodiesWritten);
            Assert.Equal(0UL, bundle.Metadata.GetLastFetchedBody());
        }

        // ---------- helpers ----------

        private sealed class TestChain
        {
            private readonly BlockHeader[] _headers;
            private readonly byte[][] _hashes;
            private readonly Sha3Keccack _keccak = new();

            private TestChain(int count)
            {
                _headers = new BlockHeader[count];
                _hashes = new byte[count][];
            }

            public static TestChain Build(int blockCount)
            {
                var chain = new TestChain(blockCount);
                byte[] prevHash = new byte[32];
                for (long n = 0; n < blockCount; n++)
                {
                    var header = MakeEmptyHeader(n, prevHash);
                    chain._headers[n] = header;
                    var encoded = BlockHeaderEncoder.Current.Encode(header);
                    var hash = chain._keccak.CalculateHash(encoded);
                    chain._hashes[n] = hash;
                    prevHash = hash;
                }
                return chain;
            }

            public BlockHeader HeaderAt(ulong n) => _headers[(int)n];
            public byte[] HashAt(ulong n) => _hashes[(int)n];
            public int Count => _headers.Length;

            private static BlockHeader MakeEmptyHeader(long blockNumber, byte[] parentHash) =>
                new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)blockNumber),
                    ParentHash = (byte[])parentHash.Clone(),
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
        }

        private sealed class ReverseScheduler : IFetchRequestScheduler
        {
            private readonly TestChain _chain;
            public int HeaderCalls;
            public int BodyCalls;
            public bool AlwaysFailHeaders;
            public bool PoisonFirstHeaderBatch;
            public bool FailBodies;
            private int _poisonFired;

            public ReverseScheduler(TestChain chain)
            {
                _chain = chain;
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(
                ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            {
                Interlocked.Increment(ref HeaderCalls);
                if (AlwaysFailHeaders)
                    throw new System.IO.IOException("simulated transport failure");

                if (!reverse)
                    throw new InvalidOperationException("BackwardBlockWalker must call with reverse: true");

                if (PoisonFirstHeaderBatch && Interlocked.CompareExchange(ref _poisonFired, 1, 0) == 0)
                {
                    // Return a single bogus header so anchor check fails.
                    var bogus = new BlockHeader
                    {
                        BlockNumber = new EvmUInt256(startBlock),
                        ParentHash = new byte[32],
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
                    return Task.FromResult(new List<BlockHeader> { bogus });
                }

                var result = new List<BlockHeader>((int)limit);
                for (ulong i = 0; i < limit; i++)
                {
                    long n = (long)startBlock - (long)i;
                    if (n < 0 || n >= _chain.Count) break;
                    result.Add(_chain.HeaderAt((ulong)n));
                }
                return Task.FromResult(result);
            }

            public Task<List<BlockBody>> FetchBodiesAsync(
                IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<BodyFetchResult> FetchBodiesAsync(
                IReadOnlyList<byte[]> blockHashes,
                IReadOnlyCollection<Guid>? excludePeers,
                CancellationToken ct)
            {
                Interlocked.Increment(ref BodyCalls);
                if (FailBodies)
                    throw new System.IO.IOException("simulated body fetch failure");

                var bodies = new List<BlockBody>(blockHashes.Count);
                for (int i = 0; i < blockHashes.Count; i++)
                    bodies.Add(new BlockBody());

                return Task.FromResult(new BodyFetchResult(bodies, new HashSet<Guid> { Guid.Empty }));
            }

            public Task<List<List<Receipt>>> FetchReceiptsAsync(
                IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<AccountRangeMessage> FetchAccountRangeAsync(
                byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<StorageRangesMessage> FetchStorageRangesAsync(
                byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<ByteCodesMessage> FetchByteCodesAsync(
                List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<TrieNodesMessage> FetchTrieNodesAsync(
                byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
                => throw new NotImplementedException();
        }
    }
}
