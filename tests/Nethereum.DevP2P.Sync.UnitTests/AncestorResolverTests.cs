using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class AncestorResolverTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        [Fact]
        public async Task FindAsync_AllBlocksMatch_ReturnsDivergedBlock()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var canonical = TestChain.Build(blockCount: 100);
            await SeedAsync(bundle, canonical, 0, 99);

            var scheduler = new HeaderProbeScheduler(canonical);
            var resolver = new AncestorResolver(scheduler, bundle, NullLogger<AncestorResolver>.Instance);

            var ancestor = await resolver.FindAsync(divergedBlock: 50, floorBlock: 10, CancellationToken.None);

            Assert.Equal(50UL, ancestor);
        }

        [Fact]
        public async Task FindAsync_DivergenceAtMidpoint_ReturnsLastMatching()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var canonical = TestChain.Build(blockCount: 100);
            var localFork = TestChain.Build(blockCount: 100, salt: 0xAB);
            await SeedAsync(bundle, canonical, 0, 30);
            await SeedAsync(bundle, localFork, 31, 60);

            var scheduler = new HeaderProbeScheduler(canonical);
            var resolver = new AncestorResolver(scheduler, bundle, NullLogger<AncestorResolver>.Instance);

            var ancestor = await resolver.FindAsync(divergedBlock: 60, floorBlock: 0, CancellationToken.None);

            Assert.Equal(30UL, ancestor);
        }

        [Fact]
        public async Task FindAsync_AncestorAtFloor_ReturnsFloor()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var canonical = TestChain.Build(blockCount: 100);
            var localFork = TestChain.Build(blockCount: 100, salt: 0xCD);
            await SeedAsync(bundle, canonical, 0, 10);
            await SeedAsync(bundle, localFork, 11, 50);

            var scheduler = new HeaderProbeScheduler(canonical);
            var resolver = new AncestorResolver(scheduler, bundle, NullLogger<AncestorResolver>.Instance);

            var ancestor = await resolver.FindAsync(divergedBlock: 50, floorBlock: 10, CancellationToken.None);

            Assert.Equal(10UL, ancestor);
        }

        [Fact]
        public async Task FindAsync_PeerReturnsEmpty_TreatedAsMismatch_StillTerminates()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var canonical = TestChain.Build(blockCount: 100);
            await SeedAsync(bundle, canonical, 0, 30);

            var scheduler = new HeaderProbeScheduler(canonical) { AlwaysReturnEmpty = true };
            var resolver = new AncestorResolver(scheduler, bundle, NullLogger<AncestorResolver>.Instance);

            var ancestor = await resolver.FindAsync(divergedBlock: 30, floorBlock: 5, CancellationToken.None);

            Assert.Equal(5UL, ancestor);
            Assert.True(scheduler.HeaderCalls >= 1);
        }

        [Fact]
        public async Task FindAsync_PeerThrows_TreatedAsMismatch_NotPropagated()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var canonical = TestChain.Build(blockCount: 100);
            await SeedAsync(bundle, canonical, 0, 30);

            var scheduler = new HeaderProbeScheduler(canonical) { AlwaysThrow = true };
            var resolver = new AncestorResolver(scheduler, bundle, NullLogger<AncestorResolver>.Instance);

            var ancestor = await resolver.FindAsync(divergedBlock: 30, floorBlock: 5, CancellationToken.None);

            Assert.Equal(5UL, ancestor);
        }

        [Fact]
        public async Task FindAsync_FloorEqualsDiverged_ReturnsFloorImmediately()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var canonical = TestChain.Build(blockCount: 100);
            await SeedAsync(bundle, canonical, 0, 30);

            var scheduler = new HeaderProbeScheduler(canonical);
            var resolver = new AncestorResolver(scheduler, bundle, NullLogger<AncestorResolver>.Instance);

            var ancestor = await resolver.FindAsync(divergedBlock: 20, floorBlock: 20, CancellationToken.None);

            Assert.Equal(20UL, ancestor);
            Assert.Equal(0, scheduler.HeaderCalls);
        }

        private static async Task SeedAsync(
            Nethereum.CoreChain.Storage.IChainStoreBundle bundle, TestChain chain, ulong from, ulong to)
        {
            for (ulong n = from; n <= to; n++)
            {
                await bundle.Blocks.SaveAsync(chain.HeaderAt(n), chain.HashAt(n));
            }
        }

        private sealed class TestChain
        {
            private readonly BlockHeader[] _headers;
            private readonly byte[][] _hashes;

            private TestChain(int count)
            {
                _headers = new BlockHeader[count];
                _hashes = new byte[count][];
            }

            public static TestChain Build(int blockCount, byte salt = 0)
            {
                var chain = new TestChain(blockCount);
                var keccak = new Sha3Keccack();
                byte[] prevHash = new byte[32];
                if (salt != 0) prevHash[0] = salt;
                for (long n = 0; n < blockCount; n++)
                {
                    var header = MakeHeader(n, prevHash, salt);
                    chain._headers[n] = header;
                    var encoded = BlockHeaderEncoder.Current.Encode(header);
                    var hash = keccak.CalculateHash(encoded);
                    chain._hashes[n] = hash;
                    prevHash = hash;
                }
                return chain;
            }

            public BlockHeader HeaderAt(ulong n) => _headers[(int)n];
            public byte[] HashAt(ulong n) => _hashes[(int)n];

            private static BlockHeader MakeHeader(long blockNumber, byte[] parentHash, byte salt)
            {
                var extra = salt == 0 ? Array.Empty<byte>() : new[] { salt };
                return new BlockHeader
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
                    ExtraData = extra,
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    LogsBloom = new byte[256],
                    Coinbase = "0x0000000000000000000000000000000000000000",
                };
            }
        }

        private sealed class HeaderProbeScheduler : IFetchRequestScheduler
        {
            private readonly TestChain _chain;
            public int HeaderCalls;
            public bool AlwaysReturnEmpty;
            public bool AlwaysThrow;

            public HeaderProbeScheduler(TestChain chain)
            {
                _chain = chain;
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(
                ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            {
                Interlocked.Increment(ref HeaderCalls);
                if (AlwaysThrow) throw new System.IO.IOException("simulated transport failure");
                if (AlwaysReturnEmpty) return Task.FromResult(new List<BlockHeader>());

                var result = new List<BlockHeader>((int)limit);
                for (ulong i = 0; i < limit; i++)
                {
                    long n = (long)startBlock + (long)i;
                    if (n < 0) continue;
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
                => throw new NotImplementedException();
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
