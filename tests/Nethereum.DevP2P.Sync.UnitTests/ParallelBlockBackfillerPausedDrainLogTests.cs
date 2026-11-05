using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
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
    public class ParallelBlockBackfillerPausedDrainLogTests
    {
        private const string DrainBlockedFragment = "persist drain blocked";

        private static readonly byte[] EmptyTrieRoot =
            "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();
        private static readonly byte[] EmptyUnclesHash =
            "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray();

        [Fact]
        public async Task Given_ABackfillPausedToYieldToExecution_When_ThePersistDrainWaits_Then_NoDrainBlockedWarningIsLogged()
        {
            var log = new CapturingLog();
            await RunBackfillWithUnfetchedBlocksAsync(yieldToExecution: true, log);

            Assert.Equal(0, log.Count(DrainBlockedFragment));
        }

        [Fact]
        public async Task Given_ABackfillNotPausedWhoseNextBlockIsUnfetched_When_ThePersistDrainWaits_Then_TheDrainBlockedWarningIsLogged()
        {
            var log = new CapturingLog();
            await RunBackfillWithUnfetchedBlocksAsync(yieldToExecution: false, log);

            Assert.True(log.Count(DrainBlockedFragment) > 0, "an unpaused drain waiting on an unfetched block must still warn");
        }

        private static async Task RunBackfillWithUnfetchedBlocksAsync(bool yieldToExecution, CapturingLog log)
        {
            var chain = BuildChain(blockCount: 5);
            using var inner = InMemoryChainStoreBundle.Open();
            for (int n = 0; n < 5; n++)
                await inner.Blocks.SaveAsync(chain.Headers[n], chain.Hashes[n]);

            var bundle = new YieldingBundle(inner, yieldToExecution);
            var backfiller = new ParallelBlockBackfiller(
                new UnusedScheduler(), new EmptyPeerPool(), new UnusedWorker(), bundle, logger: log);
            backfiller.SetPersistWaitLogIntervalForTest(TimeSpan.FromMilliseconds(5));

            using var cts = yieldToExecution
                ? new CancellationTokenSource(TimeSpan.FromMilliseconds(400))
                : new CancellationTokenSource(TimeSpan.FromSeconds(60));
            if (!yieldToExecution) log.CancelWhenLogged(DrainBlockedFragment, cts);
            await backfiller.BackfillAsync(0, 4, headersFromStore: true, cts.Token);
        }

        private sealed class CapturingLog : ILogger
        {
            private readonly List<string> _messages = new();
            private string? _cancelFragment;
            private CancellationTokenSource? _cancelSource;

            public void CancelWhenLogged(string fragment, CancellationTokenSource source)
            {
                _cancelFragment = fragment;
                _cancelSource = source;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var text = formatter(state, exception);
                lock (_messages) _messages.Add(text);
                if (_cancelFragment != null && text.Contains(_cancelFragment, StringComparison.Ordinal))
                    _cancelSource!.Cancel();
            }

            public int Count(string fragment)
            {
                lock (_messages) return _messages.Count(m => m.Contains(fragment, StringComparison.Ordinal));
            }
        }

        private sealed class Chain
        {
            public BlockHeader[] Headers = Array.Empty<BlockHeader>();
            public byte[][] Hashes = Array.Empty<byte[]>();
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
                prevHash = hash;
            }
            return c;
        }

        private sealed class YieldingBundle : IChainStoreBundle, IBackfillPauseControl
        {
            private readonly IChainStoreBundle _inner;
            private readonly bool _yieldToExecution;

            public YieldingBundle(IChainStoreBundle inner, bool yieldToExecution)
            {
                _inner = inner;
                _yieldToExecution = yieldToExecution;
            }

            public bool ShouldPauseBackfill() => false;
            public string DescribeBackfillPause() => "test: not paused";
            public bool ShouldYieldToExecution() => _yieldToExecution;
            public string DescribeYieldToExecution() => "test: yielding to block execution";

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

        private sealed class UnusedWorker : IPeerRequestWorker
        {
            public Task<List<BlockBody>> GetBodiesAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
                => throw new NotImplementedException();
            public Task<List<List<Receipt>>> GetReceiptsAsync(IEthPeer peer, IReadOnlyList<byte[]> hashes, CancellationToken ct)
                => throw new NotImplementedException();
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
