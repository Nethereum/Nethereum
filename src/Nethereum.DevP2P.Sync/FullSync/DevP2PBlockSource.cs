using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Sync;
using Nethereum.DevP2P.Sync.Scheduling;
using Nethereum.Model;
using Nethereum.Model.P2P;
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
                var headers = await LoadVerifiedHeadersAsync(cursor, ct).ConfigureAwait(false);
                if (headers is null) yield break;

                var passStart = cursor;
                for (int batchStart = 0; batchStart < headers.Count; batchStart += _bodyBatchSize)
                {
                    int take = Math.Min(_bodyBatchSize, headers.Count - batchStart);
                    var subHeaders = headers.GetRange(batchStart, take);

                    var fetched = await FetchValidBodiesAsync(subHeaders, HashHeaders(subHeaders), ct).ConfigureAwait(false);
                    if (fetched is null) yield break;
                    var (bodies, paired, bodyMismatch) = fetched.Value;
                    if (bodyMismatch) break;

                    int yielded = 0;
                    for (int i = 0; i < paired; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var header = subHeaders[i];
                        yield return BundleOf(header, bodies[i]);
                        cursor = (ulong)header.BlockNumber + 1;
                        yielded++;
                    }

                    if (yielded < subHeaders.Count)
                    {
                        _logger.LogInformation("partial body batch: {Paired}/{Requested}; restarting at block {Cursor}",
                            yielded, subHeaders.Count, cursor);
                        break;
                    }
                }

                if (cursor == passStart)
                {
                    _logger.LogInformation("source exhausted at block {Cursor}: no peer delivered a valid body", cursor);
                    yield break;
                }
            }
        }

        private async Task<List<BlockHeader>> LoadVerifiedHeadersAsync(ulong cursor, CancellationToken ct)
        {
            var headers = await _scheduler.FetchAnnouncedHeadersAsync(cursor, (ulong)_headerBatchSize, ct)
                .ConfigureAwait(false);
            if (headers is null || headers.Count == 0)
            {
                _logger.LogInformation("source exhausted at block {Cursor}", cursor);
                return null;
            }

            var expectedParent = await _parentHashLookup(cursor).ConfigureAwait(false);
            var precomputedHashes = HashHeaders(headers);

            if (RecordChainBreak(cursor, headers, expectedParent)) return null;

            if (!BlockBatchValidator.ValidateParentChain(headers, precomputedHashes, anchorHash: null, out var brokenAt))
            {
                _logger.LogWarning("intra-batch parent break at block {BlockNumber}; truncating batch to {KeepCount}",
                    (ulong)headers[brokenAt].BlockNumber, brokenAt);
                headers = headers.GetRange(0, brokenAt);
            }

            return headers.Count == 0 ? null : headers;
        }

        private bool RecordChainBreak(ulong cursor, List<BlockHeader> headers, byte[] expectedParent)
        {
            if (expectedParent is null || headers[0].ParentHash is null
                || ByteUtil.AreEqual(expectedParent, headers[0].ParentHash))
                return false;

            _lastChainBreak = new DivergenceSignal(
                AtBlock: cursor,
                PeerParentHash: headers[0].ParentHash,
                OurParentHash: expectedParent,
                QuorumPeerCount: 1,
                SourceName: _sourceName);
            _logger.LogWarning("chain-break detected: block={Cursor} our_parent=0x{OurParent} peer_parent=0x{PeerParent}",
                cursor,
                FetchRequestScheduler.HexShort(expectedParent),
                FetchRequestScheduler.HexShort(headers[0].ParentHash));
            return true;
        }

        private async Task<(IList<BlockBody> Bodies, int Paired, bool BodyMismatch)?> FetchValidBodiesAsync(
            List<BlockHeader> subHeaders, List<byte[]> hashes, CancellationToken ct)
        {
            IList<BlockBody> bodies = null;
            bool bodyMismatch = false;
            int paired = 0;
            var blamedPeers = new HashSet<Guid>();
            var peerFailureCounts = new Dictionary<Guid, int>();
            for (int attempt = 0; attempt <= MaxBodyFetchRetries; attempt++)
            {
                var fetchResult = await _scheduler.FetchBodiesAsync(
                    hashes, ExcludingPeersBehind((ulong)subHeaders[subHeaders.Count - 1].BlockNumber, blamedPeers), ct)
                    .ConfigureAwait(false);
                bodies = fetchResult?.Bodies;
                if (bodies is null) return null;
                paired = Math.Min(subHeaders.Count, bodies.Count);
                bodyMismatch = !BlockBatchValidator.ValidateBodies(
                    subHeaders, bodies, paired, _rootsProvider, LogBodyMismatch);
                if (!bodyMismatch) break;

                int newlyBlamed = BlameServingPeers(fetchResult!.ServingPeerIds, peerFailureCounts, blamedPeers);

                if (attempt < MaxBodyFetchRetries)
                {
                    _logger.LogInformation(
                        "body batch invalid — {Blamed} peer(s) discarded; retrying (attempt {Attempt}/{Max}) for {Count} blocks starting {Start}",
                        newlyBlamed,
                        attempt + 2, MaxBodyFetchRetries + 1, subHeaders.Count, (ulong)subHeaders[0].BlockNumber);
                }
            }
            return (bodies, paired, bodyMismatch);
        }

        private HashSet<Guid> ExcludingPeersBehind(ulong block, HashSet<Guid> blamedPeers)
        {
            var excluded = new HashSet<Guid>(blamedPeers);
            foreach (var peer in _pool.ActivePeers)
                if (peer.PeerLatestBlock < block) excluded.Add(peer.Id);

            return _pool.ActivePeers.Any(peer => !excluded.Contains(peer.Id)) ? excluded : blamedPeers;
        }

        private static int BlameServingPeers(
            IEnumerable<Guid> servingPeerIds, Dictionary<Guid, int> peerFailureCounts, HashSet<Guid> blamedPeers)
        {
            int newlyBlamed = 0;
            foreach (var pid in servingPeerIds)
            {
                peerFailureCounts.TryGetValue(pid, out var prior);
                var count = prior + 1;
                peerFailureCounts[pid] = count;
                if (count > SamePeerRetryTolerance && blamedPeers.Add(pid))
                    newlyBlamed++;
            }
            return newlyBlamed;
        }

        private void LogBodyMismatch(BlockBatchValidator.BodyMismatch mismatch)
        {
            if (!mismatch.TxRootOk)
            {
                _logger.LogWarning(
                    "body mismatch: block={Block} computed_tx_root=0x{Computed} header_tx_root=0x{Expected}",
                    (ulong)mismatch.Header.BlockNumber,
                    FetchRequestScheduler.HexShort(mismatch.ComputedTxRoot),
                    mismatch.Header.TransactionsHash != null ? FetchRequestScheduler.HexShort(mismatch.Header.TransactionsHash) : "<null>");
            }
            else if (!mismatch.UnclesOk)
            {
                _logger.LogWarning(
                    "uncles mismatch: block={Block} computed=0x{Computed} header=0x{Expected}",
                    (ulong)mismatch.Header.BlockNumber,
                    FetchRequestScheduler.HexShort(mismatch.ComputedUnclesHash),
                    mismatch.Header.UnclesHash != null ? FetchRequestScheduler.HexShort(mismatch.Header.UnclesHash) : "<null>");
            }
        }

        private static BlockBundle BundleOf(BlockHeader header, BlockBody body)
        {
            return new BlockBundle(
                header,
                body?.Transactions ?? new List<ISignedTransaction>(),
                body?.Uncles ?? new List<BlockHeader>(),
                body?.Withdrawals,
                HeaderHash: RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header));
        }

        private static List<byte[]> HashHeaders(List<BlockHeader> headers)
        {
            var hashes = new List<byte[]>(headers.Count);
            foreach (var header in headers)
                hashes.Add(RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header));
            return hashes;
        }

        public ValueTask DisposeAsync() => default;
    }
}
