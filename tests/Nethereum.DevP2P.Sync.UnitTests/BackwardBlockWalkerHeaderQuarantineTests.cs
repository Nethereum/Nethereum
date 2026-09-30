using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Rlpx;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Scheduling;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class BackwardBlockWalkerHeaderQuarantineTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();


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
                    var header = MakeHeader(n, prevHash, marker: 0x00);
                    chain._headers[n] = header;
                    var hash = chain._keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                    chain._hashes[n] = hash;
                    prevHash = hash;
                }
                return chain;
            }

            public BlockHeader HeaderAt(ulong n) => _headers[(int)n];
            public byte[] HashAt(ulong n) => _hashes[(int)n];
            public int Count => _headers.Length;
        }

        private static BlockHeader BogusHeaderAt(long blockNumber) =>
            MakeHeader(blockNumber, parentHash: FilledHash(0xAA), marker: 0xEE);

        private static BlockHeader MakeHeader(long blockNumber, byte[] parentHash, byte marker) =>
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
                ExtraData = new[] { marker },
                MixHash = new byte[32],
                Nonce = new byte[8],
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
            };

        private static byte[] FilledHash(byte b)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = b;
            return h;
        }


        private sealed class FakeEthPeer : IEthPeer
        {
            public FakeEthPeer(string enode) { Enode = enode; Host = enode; }
            public Guid Id { get; } = Guid.NewGuid();
            public string Enode { get; }
            public string Host { get; }
            public int EthVersion => 68;
            public ulong PeerLatestBlock => 1_000_000UL;
            public uint PeerForkHash => 0;
            public RlpxConnection Connection => null!;
            public event EventHandler<IEthPeer>? Disconnected;
        }

        private sealed class FakePeerPool : IPeerPool
        {
            private readonly List<IEthPeer> _peers;
            public FakePeerPool(IEnumerable<IEthPeer> peers) => _peers = peers.ToList();
            public IReadOnlyCollection<IEthPeer> ActivePeers => _peers;
            public int TargetPeerCount => _peers.Count;
            public event EventHandler<IEthPeer>? PeerAdded;
            public event EventHandler<IEthPeer>? PeerRemoved;
            public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
            public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
            public void ReportSuccess(Guid peerId) { }
            public Task ClearAllBansAsync() => Task.CompletedTask;
            public ValueTask DisposeAsync() => default;
        }

        private sealed class TwoPeerHeaderWorker : IPeerRequestWorker
        {
            private readonly Guid _badPeerId;
            private readonly TestChain _chain;
            public int BadPeerCalls;
            public int GoodPeerCalls;

            public TwoPeerHeaderWorker(Guid badPeerId, TestChain chain)
            {
                _badPeerId = badPeerId;
                _chain = chain;
            }

            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                if (!reverse) throw new InvalidOperationException("BackwardBlockWalker must call with reverse: true");

                if (peer.Id == _badPeerId)
                {
                    Interlocked.Increment(ref BadPeerCalls);
                    var result = new List<BlockHeader> { _chain.HeaderAt(startBlock) };
                    for (ulong i = 1; i < limit; i++)
                    {
                        long n = (long)startBlock - (long)i;
                        if (n < 0) break;
                        result.Add(BogusHeaderAt(n));
                    }
                    return Task.FromResult(result);
                }

                Interlocked.Increment(ref GoodPeerCalls);
                var real = new List<BlockHeader>((int)limit);
                for (ulong i = 0; i < limit; i++)
                {
                    long n = (long)startBlock - (long)i;
                    if (n < 0 || n >= _chain.Count) break;
                    real.Add(_chain.HeaderAt((ulong)n));
                }
                return Task.FromResult(real);
            }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        private sealed class SinglePeerHeaderWorker : IPeerRequestWorker
        {
            private readonly TestChain _chain;
            public SinglePeerHeaderWorker(TestChain chain) => _chain = chain;

            public Task<List<BlockHeader>> GetHeadersAsync(
                IEthPeer peer, ulong startBlock, ulong limit, bool reverse, CancellationToken ct)
            {
                if (!reverse) throw new InvalidOperationException("BackwardBlockWalker must call with reverse: true");
                var real = new List<BlockHeader>((int)limit);
                for (ulong i = 0; i < limit; i++)
                {
                    long n = (long)startBlock - (long)i;
                    if (n < 0 || n >= _chain.Count) break;
                    real.Add(_chain.HeaderAt((ulong)n));
                }
                return Task.FromResult(real);
            }

            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> blockHashes, CancellationToken ct) => throw new NotImplementedException();
            public Task<AccountRangeMessage> GetAccountRangeAsync(IEthPeer peer, byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(IEthPeer peer, byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(IEthPeer peer, List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(IEthPeer peer, byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct) => throw new NotImplementedException();
        }

        [Fact]
        public async Task StructurallyInvalidBatch_QuarantinesServingPeer_AndWalkerRotatesToOtherPeer_MakesProgress()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 20);

            var badPeer = new FakeEthPeer("enode://bad@127.0.0.1:1");
            var goodPeer = new FakeEthPeer("enode://good@127.0.0.1:2");
            var pool = new FakePeerPool(new IEthPeer[] { badPeer, goodPeer });
            var worker = new TwoPeerHeaderWorker(badPeer.Id, chain);
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions())
            {
                HeaderBatchQuarantineDuration = TimeSpan.FromSeconds(2),
            };

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 5,
                    MaxAnchorRetries = 5,
                    PeerRetryDelay = TimeSpan.Zero,
                    HeadersOnly = true,
                },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 19;
            var result = await walker.WalkAsync(
                fromBlock, chain.HashAt(fromBlock), toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(WalkerExitReason.StructuralGenesis, result.ExitReason);
            Assert.Equal(0UL, result.SkeletonBottomBlock);

            Assert.True(worker.GoodPeerCalls >= 1, "the serving (good) peer must actually have been reached");
            Assert.Equal(1, worker.BadPeerCalls);
            Assert.True(scheduler.IsHeaderPeerQuarantined(badPeer.Id), "the structurally-invalid peer must be quarantined");
            Assert.False(scheduler.IsHeaderPeerQuarantined(goodPeer.Id), "the honest peer must never be quarantined");
        }

        [Fact]
        public async Task StructurallyInvalidBatch_WithoutQuarantineWiring_SameBadPeerIsReselectedEveryRetry_WalkFails()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 20);

            var badPeer = new FakeEthPeer("enode://bad@127.0.0.1:1");
            var pool = new FakePeerPool(new IEthPeer[] { badPeer });
            var worker = new TwoPeerHeaderWorker(badPeer.Id, chain);
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions())
            {
                HeaderBatchQuarantineDuration = TimeSpan.FromSeconds(2),
            };

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions
                {
                    HeaderBatchSize = 5,
                    MaxAnchorRetries = 3,
                    PeerRetryDelay = TimeSpan.Zero,
                    HeadersOnly = true,
                },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 19;
            var result = await walker.WalkAsync(
                fromBlock, chain.HashAt(fromBlock), toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(WalkerExitReason.PeerPoolEmpty, result.ExitReason);
            Assert.Equal(3, worker.BadPeerCalls);
            Assert.True(worker.BadPeerCalls > 1, "the sole bad peer must be re-selected across retries, not contacted only once");
            Assert.True(scheduler.IsHeaderPeerQuarantined(badPeer.Id));
        }

        [Fact]
        public async Task LegitimateReorgDivergence_DoesNotQuarantineThePeer_PeerRemainsSelectable()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = TestChain.Build(blockCount: 500);

            var peer = new FakeEthPeer("enode://honest@127.0.0.1:1");
            var pool = new FakePeerPool(new IEthPeer[] { peer });
            var worker = new SinglePeerHeaderWorker(chain);
            var scheduler = new FetchRequestScheduler(pool, worker, new FetchRequestSchedulerOptions())
            {
                HeaderBatchQuarantineDuration = TimeSpan.FromMilliseconds(5),
            };

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 100, HeadersOnly = true },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 499;
            ulong divergentBlock = 300;
            var differentHash = FilledHash(0xDE);

            var result = await walker.WalkAsync(
                fromBlock, chain.HashAt(fromBlock), toBlockNumber: 0,
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

            Assert.False(scheduler.IsHeaderPeerQuarantined(peer.Id), "a legitimate reorg must never quarantine the serving peer");

            var (headers, servingPeerId) = await scheduler.FetchHeadersWithPeerAsync(100, 10, CancellationToken.None, reverse: true);
            Assert.Equal(peer.Id, servingPeerId);
            Assert.NotEmpty(headers);
        }
    }
}
