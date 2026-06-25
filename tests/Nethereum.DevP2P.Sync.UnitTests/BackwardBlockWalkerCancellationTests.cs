using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Tests for E-1: body channel drain-on-exception. When the header loop
    /// throws a non-cancellation exception after queuing body batches, those
    /// batches must be drained (worker processes them) before the channel is
    /// closed and the bodyCts cancelled. Otherwise headers land without
    /// matching bodies and the restart cursor re-walks already-known headers.
    /// </summary>
    public class BackwardBlockWalkerCancellationTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        [Fact]
        public async Task BodyChannel_DrainsOnException_HeadersNotOrphaned()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 200);
            var scheduler = new RecordingScheduler(chain);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 32,
                    MaxQueuedBodyBatches = 4,
                    PeerRetryDelay = TimeSpan.Zero,
                },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 199;
            var fromHash = chain.HashAt(fromBlock);

            // Header lookup throws on the second batch — the FIRST batch's
            // bodies are already queued for the worker by then.
            int lookupCalls = 0;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                walker.WalkAsync(
                    fromBlock, fromHash, toBlockNumber: 0,
                    lookupLocalBlock: (n, ct) =>
                    {
                        var call = Interlocked.Increment(ref lookupCalls);
                        if (call >= 2) throw new InvalidOperationException("simulated bundle outage on batch 2");
                        return Task.FromResult<(byte[]? hash, bool exists)>((null, false));
                    },
                    CancellationToken.None));

            // Drain semantics: body fetcher saw at least one batch (the first,
            // queued before the exception fired on batch 2).
            Assert.True(scheduler.BodyCalls >= 1,
                $"body worker should have drained the queued batch before exception propagation; BodyCalls={scheduler.BodyCalls}");

            // Body cursor was advanced — bodies are not orphaned relative to
            // headers for the first persisted batch.
            var bodyCursor = bundle.Metadata.GetLastFetchedBody();
            var headerCursor = bundle.Metadata.GetLastFetchedHeader();
            Assert.True(bodyCursor > 0,
                $"body cursor should have advanced; bodyCursor={bodyCursor}");
            Assert.True(bodyCursor <= headerCursor || headerCursor == 0,
                $"body cursor should not be ahead of header cursor; body={bodyCursor} header={headerCursor}");
        }

        [Fact]
        public async Task BodyChannel_CancellationPropagates_NoHang()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 100);
            var scheduler = new RecordingScheduler(chain) { AlwaysFailHeaders = true };

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 16,
                    MaxAnchorRetries = 10_000,
                    PeerRetryDelay = TimeSpan.FromMilliseconds(20),
                },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 99;
            var fromHash = chain.HashAt(fromBlock);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            var task = walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                cts.Token);

            var raceWinner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.Same(task, raceWinner);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
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

        private sealed class RecordingScheduler : IFetchRequestScheduler
        {
            private readonly TestChain _chain;
            public int HeaderCalls;
            public int BodyCalls;
            public bool AlwaysFailHeaders;

            public RecordingScheduler(TestChain chain) { _chain = chain; }

            public Task<List<BlockHeader>> FetchHeadersAsync(
                ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            {
                Interlocked.Increment(ref HeaderCalls);
                if (AlwaysFailHeaders) throw new System.IO.IOException("simulated transport failure");
                if (!reverse)
                    throw new InvalidOperationException("BackwardBlockWalker must call with reverse: true");

                var result = new List<BlockHeader>((int)limit);
                for (ulong i = 0; i < limit; i++)
                {
                    long n = (long)startBlock - (long)i;
                    if (n < 0 || n >= _chain.Count) break;
                    result.Add(_chain.HeaderAt((ulong)n));
                }
                return Task.FromResult(result);
            }

            public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
                => throw new NotImplementedException();

            public Task<BodyFetchResult> FetchBodiesAsync(
                IReadOnlyList<byte[]> blockHashes,
                IReadOnlyCollection<Guid>? excludePeers,
                CancellationToken ct)
            {
                Interlocked.Increment(ref BodyCalls);
                var bodies = new List<BlockBody>(blockHashes.Count);
                for (int i = 0; i < blockHashes.Count; i++)
                    bodies.Add(new BlockBody());
                return Task.FromResult(new BodyFetchResult(bodies, new HashSet<Guid> { Guid.Empty }));
            }

            public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
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
