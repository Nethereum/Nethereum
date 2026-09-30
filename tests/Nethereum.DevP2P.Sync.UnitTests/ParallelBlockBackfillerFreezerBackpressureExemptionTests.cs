using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P;
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
    public class ParallelBlockBackfillerFreezerBackpressureExemptionTests
    {
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task Given_HistoryWriteBackpressureAlwaysReportsPause_When_Backfill_Then_FreezeEligibleBlocksStillPersistPromptly()
        {
            var chain = BuildChain(blockCount: 10);
            using var inner = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < chain.Headers.Length; n++)
                await inner.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);

            var bundle = new AlwaysPausedFreezerExemptBundle(inner);
            var backfiller = new ParallelBlockBackfiller(new UnusedScheduler(), new OnePeerPool(), new ServingWorker(chain), bundle);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await backfiller.BackfillAsync(3, 8, headersFromStore: true, cts.Token);
            sw.Stop();

            Assert.True(result.Ran, "the drain must complete promptly instead of hanging behind a valve that never clears");
            Assert.Equal(6UL, result.BlocksWritten);
            Assert.Equal(new ulong[] { 3, 4, 5, 6, 7, 8 }, bundle.ExemptedBlockNumbers.OrderBy(n => n).ToArray());
            Assert.Empty(bundle.PausableBlockNumbers);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"expected a prompt drain, took {sw.Elapsed}");
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
            var roots = PatriciaBlockRootsProvider.Instance;
            var c = new Chain { Headers = new BlockHeader[blockCount], Hashes = new byte[blockCount][] };

            byte[] prevHash = new byte[32];
            for (long n = 0; n < blockCount; n++)
            {
                var txs = new List<ISignedTransaction> { MakeTx((byte)n) };
                var rcpts = new List<Receipt> { new Receipt { CumulativeGasUsed = new EvmUInt256(21000UL) } };

                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prevHash.Clone(),
                    TransactionsHash = roots.CalculateTransactionsRoot(txs),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = roots.CalculateReceiptsRoot(rcpts),
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

        private sealed class AlwaysPausedFreezerExemptBundle :
            IChainStoreBundle, IBatchedBlockPersister, IHistoryWriteBackpressure, IBackpressureExemptPersister
        {
            private readonly IChainStoreBundle _inner;
            public readonly List<ulong> ExemptedBlockNumbers = new();
            public readonly List<ulong> PausableBlockNumbers = new();

            public AlwaysPausedFreezerExemptBundle(IChainStoreBundle inner) => _inner = inner;

            public System.Numerics.BigInteger FreezeBoundary => long.MaxValue;

            public Task<int> PersistBackpressureExemptPrefixAsync(
                IReadOnlyList<PersistableBlock> blocks, System.Numerics.BigInteger freezeBoundary, CancellationToken ct = default)
            {
                foreach (var b in blocks)
                    ExemptedBlockNumbers.Add((ulong)b.Header.BlockNumber.ToBigInteger());
                return Task.FromResult(blocks.Count);
            }

            public Task PersistBlocksAsync(IReadOnlyList<PersistableBlock> blocks, CancellationToken ct = default)
            {
                foreach (var b in blocks)
                    PausableBlockNumbers.Add((ulong)b.Header.BlockNumber.ToBigInteger());
                return Task.CompletedTask;
            }

            public bool ShouldPauseHistoryWrites() => true;
            public string DescribeHistoryBackpressure() => "test: permanently paused";

            public IStateStore State => _inner.State;
            public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public Nethereum.Merkle.Patricia.Storage.ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;
            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default) => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default) => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _inner.ResolveCheckpointSnapshotPath(blockNumber);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
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
