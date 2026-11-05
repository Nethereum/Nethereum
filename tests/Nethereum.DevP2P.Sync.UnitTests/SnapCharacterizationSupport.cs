using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    internal sealed class CharLog : ILogger
    {
        public sealed record Entry(LogLevel Level, string Message, Exception Exception);

        private readonly ConcurrentQueue<Entry> _entries = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _entries.Enqueue(new Entry(logLevel, formatter(state, exception), exception));

        public IReadOnlyList<Entry> Entries => _entries.ToArray();

        public IReadOnlyList<Entry> With(string fragment)
            => _entries.Where(e => e.Message.Contains(fragment, StringComparison.Ordinal)).ToArray();

        public bool Saw(string fragment) => With(fragment).Count > 0;

        public async Task<bool> WaitForAsync(string fragment, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (Saw(fragment)) return true;
                await Task.Delay(10);
            }
            return Saw(fragment);
        }
    }

    internal sealed class CharPool : IPeerPool
    {
        public IReadOnlyCollection<IEthPeer> ActivePeers => Array.Empty<IEthPeer>();
        public int TargetPeerCount => 0;
        public event EventHandler<IEthPeer> PeerAdded { add { } remove { } }
        public event EventHandler<IEthPeer> PeerRemoved { add { } remove { } }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task BanAndDropAsync(string enode, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task DropAsync(Guid peerId, string reason, CancellationToken ct) => Task.CompletedTask;
        public void ReportSuccess(Guid peerId) { }
        public Task ClearAllBansAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal sealed class CharTipSource : ICanonicalStateRootSource
    {
        private readonly Func<CancellationToken, Task<CanonicalTip>> _latest;

        public CharTipSource(Func<CancellationToken, Task<CanonicalTip>> latest) => _latest = latest;

        public CharTipSource(CanonicalTip tip) : this(_ => Task.FromResult(tip)) { }

        public string Name => "char-tip";

        public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
            => Task.FromResult<(byte[] StateRoot, byte[] BlockHash)>((null, null));

        public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => _latest(ct);
    }

    internal sealed class CharScheduler : IFetchRequestScheduler
    {
        public Func<ulong, ulong, CancellationToken, Task<List<BlockHeader>>> Headers { get; set; }
        public Func<ulong, ulong, bool, CancellationToken, Task<List<BlockHeader>>> HeadersDirected { get; set; }
        public Func<byte[], byte[], byte[], CancellationToken, Task<AccountRangeMessage>> AccountRange { get; set; }
        public Func<byte[], List<List<byte[]>>, CancellationToken, Task<TrieNodesMessage>> TrieNodes { get; set; }
        public Func<List<byte[]>, CancellationToken, Task<ByteCodesMessage>> ByteCodes { get; set; }

        private static async Task<T> NeverAnswers<T>(CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            throw new OperationCanceledException(ct);
        }

        public Task<List<BlockHeader>> FetchHeadersAsync(ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
            => HeadersDirected != null ? HeadersDirected(startBlock, limit, reverse, ct)
                : Headers != null ? Headers(startBlock, limit, ct)
                : NeverAnswers<List<BlockHeader>>(ct);

        public Task<List<BlockBody>> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => NeverAnswers<List<BlockBody>>(ct);

        public Task<BodyFetchResult> FetchBodiesAsync(IReadOnlyList<byte[]> blockHashes, IReadOnlyCollection<Guid> excludePeers, CancellationToken ct)
            => NeverAnswers<BodyFetchResult>(ct);

        public Task<List<List<Receipt>>> FetchReceiptsAsync(IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
            => NeverAnswers<List<List<Receipt>>>(ct);

        public Task<AccountRangeMessage> FetchAccountRangeAsync(byte[] stateRoot, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => AccountRange != null ? AccountRange(stateRoot, startingHash, limitHash, ct) : NeverAnswers<AccountRangeMessage>(ct);

        public Task<StorageRangesMessage> FetchStorageRangesAsync(byte[] stateRoot, List<byte[]> accountHashes, byte[] startingHash, byte[] limitHash, ulong responseBytes, CancellationToken ct)
            => NeverAnswers<StorageRangesMessage>(ct);

        public Task<ByteCodesMessage> FetchByteCodesAsync(List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
            => ByteCodes != null ? ByteCodes(codeHashes, ct) : NeverAnswers<ByteCodesMessage>(ct);

        public Task<TrieNodesMessage> FetchTrieNodesAsync(byte[] stateRoot, List<List<byte[]>> paths, ulong responseBytes, CancellationToken ct)
            => TrieNodes != null ? TrieNodes(stateRoot, paths, ct) : NeverAnswers<TrieNodesMessage>(ct);
    }

    internal sealed class CharBundle : IChainStoreBundle
    {
        private readonly InMemoryChainStoreBundle _inner = InMemoryChainStoreBundle.Open();

        public ITrieNodeStore StateTrieNodesOverride { get; set; }
        public Func<ulong, byte[], byte[], CancellationToken, Task<ChainCheckpoint>> SaveCheckpointHook { get; set; }
        public int SaveCheckpointCalls;

        public IStateStore State => _inner.State;
        public ITrieNodeStore TrieNodes => _inner.TrieNodes;
        public ITrieNodeStore StateTrieNodes => StateTrieNodesOverride ?? _inner.StateTrieNodes;
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
        {
            Interlocked.Increment(ref SaveCheckpointCalls);
            return SaveCheckpointHook != null
                ? SaveCheckpointHook(blockNumber, stateRoot, blockHash, ct)
                : _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
        }

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

    internal sealed class CharChain
    {
        private static readonly byte[] EmptyUnclesHash = Convert.FromHexString("1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347");
        private static readonly byte[] EmptyTrieRoot = Convert.FromHexString("56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421");

        private readonly BlockHeader[] _headers;
        private readonly byte[][] _hashes;

        public CharChain(int count, IReadOnlyDictionary<ulong, byte[]> stateRoots = null)
        {
            _headers = new BlockHeader[count];
            _hashes = new byte[count][];
            var prev = new byte[32];
            for (int n = 0; n < count; n++)
            {
                var header = new BlockHeader
                {
                    BlockNumber = new EvmUInt256((ulong)n),
                    ParentHash = (byte[])prev.Clone(),
                    TransactionsHash = (byte[])EmptyTrieRoot.Clone(),
                    UnclesHash = (byte[])EmptyUnclesHash.Clone(),
                    ReceiptHash = (byte[])EmptyTrieRoot.Clone(),
                    StateRoot = stateRoots != null && stateRoots.TryGetValue((ulong)n, out var root) ? root : new byte[32],
                    Difficulty = new EvmUInt256(1UL),
                    GasLimit = 1,
                    Timestamp = 1,
                    ExtraData = Array.Empty<byte>(),
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    LogsBloom = new byte[256],
                    Coinbase = "0x0000000000000000000000000000000000000000",
                };
                _headers[n] = header;
                _hashes[n] = Nethereum.Util.Sha3Keccack.Current.CalculateHash(Nethereum.Model.BlockHeaderEncoder.Current.Encode(header));
                prev = _hashes[n];
            }
        }

        public int Count => _headers.Length;
        public BlockHeader HeaderAt(ulong n) => _headers[(int)n];
        public byte[] HashAt(ulong n) => _hashes[(int)n];

        public Task<List<BlockHeader>> Serve(ulong start, ulong limit, bool reverse)
        {
            var result = new List<BlockHeader>();
            for (ulong i = 0; i < limit; i++)
            {
                long n = reverse ? (long)start - (long)i : (long)start + (long)i;
                if (n < 0 || n >= _headers.Length) break;
                result.Add(_headers[n]);
            }
            return Task.FromResult(result);
        }
    }

    internal static class CharBytes
    {
        public static byte[] Hash32(byte seed)
        {
            var h = new byte[32];
            for (int i = 0; i < 32; i++) h[i] = (byte)(seed + i);
            return h;
        }
    }
}
