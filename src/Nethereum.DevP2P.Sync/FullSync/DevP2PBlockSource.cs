using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.FullSync
{
    public sealed class DevP2PBlockSource : IBlockSource, IAsyncDisposable
    {
        private readonly IPeerPool _pool;
        private readonly IFetchRequestScheduler _scheduler;
        private readonly Func<ulong, Task<byte[]?>> _parentHashLookup;
        private readonly int _headerBatchSize;
        private readonly int _bodyBatchSize;
        private readonly ILogger<DevP2PBlockSource> _logger;
        private readonly Sha3Keccack _keccak = new();
        private readonly PatriciaBlockRootsProvider _rootsProvider = PatriciaBlockRootsProvider.Instance;

        private const int MaxBodyFetchRetries = 2;
        private const int SamePeerRetryTolerance = 1;

        private DivergenceSignal? _lastChainBreak;
        private readonly string _sourceName;

        public DevP2PBlockSource(
            IPeerPool pool,
            IFetchRequestScheduler scheduler,
            Func<ulong, Task<byte[]?>> parentHashLookup,
            int headerBatchSize = 192,
            int bodyBatchSize = 64,
            string sourceName = "devp2p",
            ILogger<DevP2PBlockSource>? logger = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _parentHashLookup = parentHashLookup ?? throw new ArgumentNullException(nameof(parentHashLookup));
            _headerBatchSize = headerBatchSize;
            _bodyBatchSize = bodyBatchSize;
            _sourceName = sourceName;
            _logger = logger ?? NullLogger<DevP2PBlockSource>.Instance;
        }

        public DivergenceSignal LastChainBreak => _lastChainBreak!;

        public async Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
        {
            if (_pool.ActivePeers.Count == 0) return BlockSourceHealth.Unavailable;
            if (_pool.ActivePeers.Count * 2 < _pool.TargetPeerCount) return BlockSourceHealth.Degraded;
            return BlockSourceHealth.Healthy;
        }

        public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
        {
            _logger.LogWarning("bad bundle: block={BlockNumber} reason={Reason}", blockNumber, reason);
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<BlockBundle> StreamAsync(
            ulong fromBlock,
            [EnumeratorCancellation] CancellationToken ct)
        {
            _lastChainBreak = null;
            ulong cursor = fromBlock;

            while (!ct.IsCancellationRequested)
            {
                var headers = await _scheduler.FetchHeadersAsync(cursor, (ulong)_headerBatchSize, ct)
                    .ConfigureAwait(false);
                if (headers is null || headers.Count == 0)
                {
                    _logger.LogInformation("source exhausted at block {Cursor}", cursor);
                    yield break;
                }

                var expectedParent = await _parentHashLookup(cursor).ConfigureAwait(false);
                var precomputedHashes = new byte[headers.Count][];
                for (int i = 0; i < headers.Count; i++)
                    precomputedHashes[i] = HashHeader(headers[i]);

                if (expectedParent is not null && headers[0].ParentHash is not null
                    && !ByteUtil.AreEqual(expectedParent, headers[0].ParentHash))
                {
                    _lastChainBreak = new DivergenceSignal(
                        AtBlock: cursor,
                        PeerParentHash: headers[0].ParentHash,
                        OurParentHash: expectedParent,
                        QuorumPeerCount: 1,
                        SourceName: _sourceName);
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        string ourHex = expectedParent.ToHex();
                        string peerHex = headers[0].ParentHash.ToHex();
                        _logger.LogWarning("chain-break detected: block={Cursor} our_parent=0x{OurParent} peer_parent=0x{PeerParent}",
                            cursor,
                            ourHex.Substring(0, Math.Min(16, ourHex.Length)),
                            peerHex.Substring(0, Math.Min(16, peerHex.Length)));
                    }
                    yield break;
                }

                if (!BlockBatchValidator.ValidateParentChain(headers, precomputedHashes, anchorHash: null, out var brokenAt))
                {
                    _logger.LogWarning("intra-batch parent break at block {BlockNumber}; truncating batch to {KeepCount}",
                        (ulong)headers[brokenAt].BlockNumber, brokenAt);
                    headers = headers.GetRange(0, brokenAt);
                }

                if (headers.Count == 0) yield break;

                for (int batchStart = 0; batchStart < headers.Count; batchStart += _bodyBatchSize)
                {
                    int take = Math.Min(_bodyBatchSize, headers.Count - batchStart);
                    var subHeaders = headers.GetRange(batchStart, take);
                    var hashes = new List<byte[]>(take);
                    foreach (var h in subHeaders)
                        hashes.Add(HashHeader(h));

                    IList<BlockBody> bodies = null;
                    bool bodyMismatch = false;
                    int paired = 0;
                    int yielded = 0;
                    var blamedPeers = new HashSet<Guid>();
                    var peerFailureCounts = new Dictionary<Guid, int>();
                    for (int attempt = 0; attempt <= MaxBodyFetchRetries; attempt++)
                    {
                        var fetchResult = await _scheduler.FetchBodiesAsync(hashes, blamedPeers, ct).ConfigureAwait(false);
                        bodies = fetchResult?.Bodies;
                        if (bodies is null) yield break;
                        paired = Math.Min(subHeaders.Count, bodies.Count);
                        bodyMismatch = !BlockBatchValidator.ValidateBodies(
                            subHeaders, bodies, paired, _rootsProvider,
                            mismatch =>
                            {
                                if (!mismatch.TxRootOk)
                                {
                                    _logger.LogWarning(
                                        "body mismatch: block={Block} computed_tx_root=0x{Computed} header_tx_root=0x{Expected}",
                                        (ulong)mismatch.Header.BlockNumber,
                                        mismatch.ComputedTxRoot.ToHex().Substring(0, 16),
                                        mismatch.Header.TransactionsHash != null ? mismatch.Header.TransactionsHash.ToHex().Substring(0, 16) : "<null>");
                                }
                                else if (!mismatch.UnclesOk)
                                {
                                    _logger.LogWarning(
                                        "uncles mismatch: block={Block} computed=0x{Computed} header=0x{Expected}",
                                        (ulong)mismatch.Header.BlockNumber,
                                        mismatch.ComputedUnclesHash.ToHex().Substring(0, 16),
                                        mismatch.Header.UnclesHash != null ? mismatch.Header.UnclesHash.ToHex().Substring(0, 16) : "<null>");
                                }
                            });
                        if (!bodyMismatch) break;

                        int newlyBlamed = 0;
                        foreach (var pid in fetchResult!.ServingPeerIds)
                        {
                            peerFailureCounts.TryGetValue(pid, out var prior);
                            var count = prior + 1;
                            peerFailureCounts[pid] = count;
                            if (count > SamePeerRetryTolerance && blamedPeers.Add(pid))
                                newlyBlamed++;
                        }

                        if (attempt < MaxBodyFetchRetries)
                        {
                            _logger.LogInformation(
                                "body batch invalid — {Blamed} peer(s) discarded; retrying (attempt {Attempt}/{Max}) for {Count} blocks starting {Start}",
                                newlyBlamed,
                                attempt + 2, MaxBodyFetchRetries + 1, take, (ulong)subHeaders[0].BlockNumber);
                        }
                    }
                    if (bodyMismatch)
                    {
                        break;
                    }
                    for (int i = 0; i < paired; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var header = subHeaders[i];
                        var body = bodies[i];
                        var transactions = body?.Transactions ?? new List<ISignedTransaction>();
                        var bodyUncles = body?.Uncles ?? new List<BlockHeader>();
                        var bundle = new BlockBundle(
                            header,
                            transactions,
                            bodyUncles,
                            body?.Withdrawals,
                            HeaderHash: HashHeader(header));
                        yield return bundle;
                        cursor = (ulong)header.BlockNumber + 1;
                        yielded++;
                    }

                    if (bodyMismatch)
                    {
                        break;
                    }

                    if (yielded < subHeaders.Count)
                    {
                        _logger.LogInformation("partial body batch: {Paired}/{Requested}; restarting at block {Cursor}",
                            yielded, subHeaders.Count, cursor);
                        break;
                    }
                }
            }
        }

        private byte[] HashHeader(BlockHeader header)
        {
            var encoded = new BlockHeaderEncoder().Encode(header);
            return _keccak.CalculateHash(encoded);
        }

        public ValueTask DisposeAsync() => default;
    }
}
