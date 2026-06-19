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

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// <see cref="IBlockSource"/> backed by the multi-peer DevP2P stack:
    /// <see cref="IPeerPool"/> + <see cref="IFetchRequestScheduler"/>.
    /// Streams parent-hash-validated <see cref="BlockBundle"/>s.
    ///
    /// <para>On the first header of a fetched batch, compares
    /// <c>header.ParentHash</c> against the injected parent-hash lookup.
    /// A mismatch is a chain-break and the stream completes via
    /// <see cref="LastChainBreak"/>; the follower routes that through the
    /// same auto-rewind path it uses for state-root divergence.</para>
    /// </summary>
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
        private static readonly byte[] EmptyUnclesHash = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList());

        /// <summary>
        /// On body commitment-mismatch, retry the same batch this many times
        /// before falling back to the outer StreamAsync restart (which
        /// re-claims a peer + re-fetches headers). Catches transient hiccups
        /// (packet drop, peer mid-reorg, brief storage latency) without
        /// burning the expensive outer recovery path. A deterministically bad
        /// peer will fail both attempts and propagate normally.
        /// </summary>
        // 3 attempts total: tolerate one same-peer mismatch (transient incomplete body /
        // packet drop / brief storage latency), discard the peer after the second
        // consecutive failure, then rotate to a different peer on attempt #3.
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

                for (int i = 1; i < headers.Count; i++)
                {
                    var prevHash = HashHeader(headers[i - 1]);
                    if (!ByteUtil.AreEqual(prevHash, headers[i].ParentHash))
                    {
                        _logger.LogWarning("intra-batch parent break at block {BlockNumber}; truncating batch to {KeepCount}",
                            (ulong)headers[i].BlockNumber, i);
                        headers = headers.GetRange(0, i);
                        break;
                    }
                }

                if (headers.Count == 0) yield break;

                for (int batchStart = 0; batchStart < headers.Count; batchStart += _bodyBatchSize)
                {
                    int take = Math.Min(_bodyBatchSize, headers.Count - batchStart);
                    var subHeaders = headers.GetRange(batchStart, take);
                    var hashes = new List<byte[]>(take);
                    foreach (var h in subHeaders)
                        hashes.Add(HashHeader(h));

                    // Retry-then-rotate with same-peer tolerance: a first body-mismatch
                    // from a given peer can be a transient incomplete body / packet
                    // drop / brief storage latency — re-asking the same peer once is
                    // cheaper than rotating + re-claiming. After two consecutive
                    // failures from the same peer we discard them (add to the
                    // exclusion set so the scheduler picks a different peer on the
                    // next attempt).
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
                        bodyMismatch = ValidateBatch(subHeaders, bodies, paired);
                        if (!bodyMismatch) break;

                        // Bodies failed validation. Count this against every peer that
                        // served a chunk; once a peer's count exceeds the tolerance,
                        // it joins the exclusion set so the next attempt rotates off
                        // it. On the parallel-chunk path we can't tell which chunk's
                        // peer was bad, so we count against all serving peers; healthy
                        // peers in the mix will get blamed too but the cost is low
                        // (we re-claim them after the exclusion set is cleared).
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
                        // Both attempts failed validation. Restart the outer
                        // StreamAsync loop at the current cursor — gets a
                        // freshly-claimed peer and re-fetches headers from
                        // scratch in case the header chain itself is poisoned.
                        break;
                    }
                    // Validation already passed for the whole paired range;
                    // yield each (header, body) bundle and advance the cursor.
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
                        // Unreachable after retry-then-rotate restructure but
                        // kept for safety in case the outer logic changes.
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

        /// <summary>
        /// Validate each (header, body) pair in the batch against the header's
        /// TransactionsHash and UnclesHash commitments. Returns true if ANY
        /// pair fails — caller decides whether to retry or restart.
        /// </summary>
        private bool ValidateBatch(IList<BlockHeader> subHeaders, IList<BlockBody> bodies, int paired)
        {
            for (int i = 0; i < paired; i++)
            {
                var header = subHeaders[i];
                var body = bodies[i];
                var transactions = body?.Transactions ?? new List<ISignedTransaction>();
                var bodyUncles = body?.Uncles ?? new List<BlockHeader>();

                var computedTxRoot = _rootsProvider.CalculateTransactionsRoot(transactions);
                if (!ByteUtil.AreEqual(computedTxRoot, header.TransactionsHash))
                {
                    _logger.LogWarning(
                        "body mismatch: block={Block} computed_tx_root=0x{Computed} header_tx_root=0x{Expected}",
                        (ulong)header.BlockNumber,
                        computedTxRoot.ToHex().Substring(0, 16),
                        header.TransactionsHash != null ? header.TransactionsHash.ToHex().Substring(0, 16) : "<null>");
                    return true;
                }

                var computedUnclesHash = bodyUncles.Count == 0
                    ? EmptyUnclesHash
                    : ComputeUnclesHash(bodyUncles);
                if (!ByteUtil.AreEqual(computedUnclesHash, header.UnclesHash))
                {
                    _logger.LogWarning(
                        "uncles mismatch: block={Block} computed=0x{Computed} header=0x{Expected}",
                        (ulong)header.BlockNumber,
                        computedUnclesHash.ToHex().Substring(0, 16),
                        header.UnclesHash != null ? header.UnclesHash.ToHex().Substring(0, 16) : "<null>");
                    return true;
                }
            }
            return false;
        }

        private byte[] HashHeader(BlockHeader header)
        {
            var encoded = new BlockHeaderEncoder().Encode(header);
            return _keccak.CalculateHash(encoded);
        }

        private byte[] ComputeUnclesHash(IList<BlockHeader> uncles)
        {
            var encoded = new byte[uncles.Count][];
            for (int i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderEncoder.Current.Encode(uncles[i]);
            return _keccak.CalculateHash(RLP.RLP.EncodeList(encoded));
        }

        public ValueTask DisposeAsync() => default;
    }
}
