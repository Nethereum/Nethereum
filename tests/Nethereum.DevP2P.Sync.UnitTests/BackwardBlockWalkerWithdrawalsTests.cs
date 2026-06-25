using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
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
    /// W-2 / Cluster A residue: BackwardBlockWalker must persist withdrawals
    /// alongside uncles + transactions for post-Shanghai blocks, so a
    /// downstream forward-execute path can recompute the canonical block
    /// hash. Without this, OrderingBlockSource yields Withdrawals=null and
    /// the executor fatal-exits on a withdrawals_root divergence.
    /// </summary>
    public class BackwardBlockWalkerWithdrawalsTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();
        private static readonly byte[] EmptyTrieRoot =
            "56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

        [Fact]
        public async Task PostShanghaiBatch_PersistsWithdrawals_OnBundleWithdrawalStore()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var chain = WithdrawalsTestChain.Build(blockCount: 10);
            var scheduler = new WithdrawalsScheduler(chain);

            var walker = new BackwardBlockWalker(
                scheduler, bundle,
                new BackwardBlockWalkerOptions { HeaderBatchSize = 8 },
                NullLogger<BackwardBlockWalker>.Instance);

            ulong fromBlock = 9;
            var fromHash = chain.HashAt(fromBlock);

            var result = await walker.WalkAsync(
                fromBlock, fromHash, toBlockNumber: 0,
                lookupLocalBlock: (n, ct) => Task.FromResult<(byte[]? hash, bool exists)>((null, false)),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.BodiesWritten > 0);

            // Spot-check a non-empty post-Shanghai block: walker must have
            // persisted exactly the wire-shaped withdrawals through the store.
            var hash5 = chain.HashAt(5);
            var w5 = await bundle.Withdrawals.GetByBlockHashAsync(hash5);
            Assert.NotNull(w5);
            Assert.Equal(2, w5.Count);
            Assert.Equal(500UL, w5[0].Index);
            Assert.Equal(123UL, w5[0].ValidatorIndex);
            Assert.Equal(32_000_000UL, w5[0].AmountInGwei);
            Assert.Equal(501UL, w5[1].Index);
        }

        private sealed class WithdrawalsTestChain
        {
            private readonly BlockHeader[] _headers;
            private readonly byte[][] _hashes;
            private readonly List<Withdrawal>[] _withdrawals;
            private readonly Sha3Keccack _keccak = new();

            private WithdrawalsTestChain(int count)
            {
                _headers = new BlockHeader[count];
                _hashes = new byte[count][];
                _withdrawals = new List<Withdrawal>[count];
            }

            public static WithdrawalsTestChain Build(int blockCount)
            {
                var chain = new WithdrawalsTestChain(blockCount);
                byte[] prevHash = new byte[32];
                for (long n = 0; n < blockCount; n++)
                {
                    var withdrawals = new List<Withdrawal>
                    {
                        new Withdrawal
                        {
                            Index = (ulong)(100 * n),
                            ValidatorIndex = 123,
                            Address = MakeAddress((byte)n),
                            AmountInGwei = 32_000_000UL,
                        },
                        new Withdrawal
                        {
                            Index = (ulong)(100 * n + 1),
                            ValidatorIndex = 124,
                            Address = MakeAddress((byte)(n + 1)),
                            AmountInGwei = 16_000_000UL,
                        },
                    };
                    chain._withdrawals[n] = withdrawals;
                    var header = MakeEmptyHeader(n, prevHash);
                    header.WithdrawalsRoot = PatriciaBlockRootsProvider.Instance
                        .CalculateWithdrawalsRoot(withdrawals);
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
            public List<Withdrawal> WithdrawalsAt(ulong n) => _withdrawals[(int)n];
            public int Count => _headers.Length;

            private static byte[] MakeAddress(byte seed)
            {
                var a = new byte[20];
                for (int i = 0; i < 20; i++) a[i] = seed;
                return a;
            }

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

        private sealed class WithdrawalsScheduler : IFetchRequestScheduler
        {
            private readonly WithdrawalsTestChain _chain;
            private readonly Dictionary<string, ulong> _blockByHash = new();

            public WithdrawalsScheduler(WithdrawalsTestChain chain)
            {
                _chain = chain;
                for (ulong n = 0; n < (ulong)chain.Count; n++)
                {
                    _blockByHash[chain.HashAt(n).ToHex()] = n;
                }
            }

            public Task<List<BlockHeader>> FetchHeadersAsync(
                ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            {
                var result = new List<BlockHeader>((int)limit);
                if (!reverse)
                    throw new InvalidOperationException("BackwardBlockWalker must call with reverse: true");
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
                var bodies = new List<BlockBody>(blockHashes.Count);
                for (int i = 0; i < blockHashes.Count; i++)
                {
                    var hashHex = blockHashes[i].ToHex();
                    var blk = _blockByHash[hashHex];
                    bodies.Add(new BlockBody
                    {
                        Transactions = new List<ISignedTransaction>(),
                        Uncles = new List<BlockHeader>(),
                        Withdrawals = _chain.WithdrawalsAt(blk),
                    });
                }
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
