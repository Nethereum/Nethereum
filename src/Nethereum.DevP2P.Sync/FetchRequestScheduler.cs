using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Model;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Production <see cref="IFetchRequestScheduler"/>. Picks a peer per
    /// request from <see cref="IPeerPool.ActivePeers"/>, ranks by (lowest
    /// in-flight count) then (highest <see cref="PeerScore.ComputedScore"/>).
    /// On per-request timeout or exception, retries against another peer.
    /// Does NOT own pool lifetime — start/stop the pool externally. DOES own
    /// in-flight bookkeeping per peer; peers that leave the pool mid-request
    /// have their counter dropped via the PeerRemoved event.
    /// </summary>
    public sealed class FetchRequestScheduler : IFetchRequestScheduler
    {
        private readonly IPeerPool _pool;
        private readonly IPeerRequestWorker _worker;
        private readonly FetchRequestSchedulerOptions _options;
        private readonly Func<string, PeerScore>? _scoreLookup;
        private readonly ILogger<FetchRequestScheduler> _logger;

        private readonly ConcurrentDictionary<Guid, int> _inFlight = new();

        public FetchRequestScheduler(
            IPeerPool pool,
            IPeerRequestWorker worker,
            FetchRequestSchedulerOptions options,
            Func<string, PeerScore>? scoreLookup = null,
            ILogger<FetchRequestScheduler>? logger = null)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _scoreLookup = scoreLookup;
            _logger = logger ?? NullLogger<FetchRequestScheduler>.Instance;

            _pool.PeerRemoved += OnPeerRemoved;
        }

        public async Task<List<BlockHeader>> FetchHeadersAsync(
            ulong startBlock, ulong limit, CancellationToken ct)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"headers {startBlock}+{limit}",
                (peer, attemptCt) => _worker.GetHeadersAsync(peer, startBlock, limit, attemptCt),
                initialExcluded: null,
                ct).ConfigureAwait(false);
            return result;
        }

        public async Task<List<BlockBody>> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            var result = await FetchBodiesAsync(blockHashes, excludePeers: null, ct).ConfigureAwait(false);
            return result.Bodies;
        }

        public async Task<BodyFetchResult> FetchBodiesAsync(
            IReadOnlyList<byte[]> blockHashes,
            IReadOnlyCollection<Guid>? excludePeers,
            CancellationToken ct)
        {
            if (blockHashes is null) throw new ArgumentNullException(nameof(blockHashes));
            if (blockHashes.Count == 0)
                return new BodyFetchResult(new List<BlockBody>(), Array.Empty<Guid>());

            var chunkSize = _options.EffectiveBodyFetchChunkSize;
            var maxParallel = _options.EffectiveMaxParallelBodyFetches;

            // Single-chunk fast path: no fan-out work to do.
            if (blockHashes.Count <= chunkSize || maxParallel <= 1)
            {
                var (single, singlePeer) = await ExecuteWithRetryAsync(
                    $"bodies x{blockHashes.Count}",
                    (peer, attemptCt) => _worker.GetBodiesAsync(peer, blockHashes, attemptCt),
                    excludePeers,
                    ct).ConfigureAwait(false);
                return new BodyFetchResult(single, new[] { singlePeer });
            }

            // Cap parallelism by available peer count so we don't queue
            // multiple chunks against the same peer when MaxInFlightPerPeer = 1.
            var activePeerCount = _pool.ActivePeers.Count;
            if (activePeerCount <= 1)
            {
                var (single, singlePeer) = await ExecuteWithRetryAsync(
                    $"bodies x{blockHashes.Count}",
                    (peer, attemptCt) => _worker.GetBodiesAsync(peer, blockHashes, attemptCt),
                    excludePeers,
                    ct).ConfigureAwait(false);
                return new BodyFetchResult(single, new[] { singlePeer });
            }

            var effectiveParallel = Math.Min(maxParallel, activePeerCount);
            var chunks = SplitIntoChunks(blockHashes, chunkSize, effectiveParallel);

            // Each chunk independently re-uses ExecuteWithRetryAsync — preserves
            // per-chunk timeout-then-reassign semantics. Tasks run concurrently
            // and are merged back in chunk order so callers see the same flat
            // list they would have got from a single-peer request.
            var chunkTasks = new Task<(List<BlockBody> Bodies, Guid PeerId)>[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                chunkTasks[i] = ExecuteWithRetryAsync(
                    $"bodies x{chunk.Count}",
                    (peer, attemptCt) => _worker.GetBodiesAsync(peer, chunk, attemptCt),
                    excludePeers,
                    ct);
            }

            var results = await Task.WhenAll(chunkTasks).ConfigureAwait(false);

            var merged = new List<BlockBody>(blockHashes.Count);
            var servingPeerIds = new HashSet<Guid>();
            foreach (var part in results)
            {
                if (part.Bodies is not null) merged.AddRange(part.Bodies);
                servingPeerIds.Add(part.PeerId);
            }
            return new BodyFetchResult(merged, servingPeerIds);
        }

        private static List<IReadOnlyList<byte[]>> SplitIntoChunks(
            IReadOnlyList<byte[]> hashes, int chunkSize, int maxChunks)
        {
            // Aim for roughly chunkSize-sized chunks but never exceed maxChunks
            // so we don't spawn more parallel fetches than the pool supports.
            var targetChunkCount = Math.Min(maxChunks, (hashes.Count + chunkSize - 1) / chunkSize);
            if (targetChunkCount <= 1)
                return new List<IReadOnlyList<byte[]>> { hashes };

            var perChunk = (hashes.Count + targetChunkCount - 1) / targetChunkCount;
            var chunks = new List<IReadOnlyList<byte[]>>(targetChunkCount);
            for (int start = 0; start < hashes.Count; start += perChunk)
            {
                var end = Math.Min(start + perChunk, hashes.Count);
                var slice = new byte[end - start][];
                for (int j = start; j < end; j++) slice[j - start] = hashes[j];
                chunks.Add(slice);
            }
            return chunks;
        }

        private async Task<(T Result, Guid PeerId)> ExecuteWithRetryAsync<T>(
            string label,
            Func<IEthPeer, CancellationToken, Task<T>> sendOnce,
            IReadOnlyCollection<Guid>? initialExcluded,
            CancellationToken ct)
        {
            var attempts = 0;
            Exception? lastError = null;
            var triedPeers = initialExcluded is null
                ? new HashSet<Guid>()
                : new HashSet<Guid>(initialExcluded);

            while (attempts < _options.MaxRetriesPerRequest)
            {
                ct.ThrowIfCancellationRequested();
                var peer = await ClaimBestPeerAsync(triedPeers, ct).ConfigureAwait(false);
                if (peer is null)
                {
                    // All current pool members already tried (or briefly empty pool).
                    // Clear the exclusion set so previously-tried peers can be retried
                    // once the dial loop refreshes. Then wait a bit for the pool to
                    // recover.
                    if (triedPeers.Count > 0)
                    {
                        _logger.LogInformation("fetch: {Request} — all {Tried} tried peers exhausted, clearing exclusion and retrying after pool refresh", label, triedPeers.Count);
                        triedPeers.Clear();
                        try { await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                        continue;
                    }

                    lastError = new InvalidOperationException(
                        $"No peer available for {label} after {attempts} attempts.");
                    break;
                }

                triedPeers.Add(peer.Id);
                attempts++;
                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptCts.CancelAfter(_options.EffectivePerRequestTimeout);
                    var result = await sendOnce(peer, attemptCts.Token).ConfigureAwait(false);
                    return (result, peer.Id);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    lastError = new TimeoutException(
                        $"{label}: peer {MainnetPeerSession.ParseHost(peer.Enode)} timed out");
                    _logger.LogWarning("fetch timeout: {Request} peer={Host} attempt={Attempt}; reassigning",
                        label, MainnetPeerSession.ParseHost(peer.Enode), attempts);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                    _logger.LogWarning("fetch error: {Request} peer={Host} error={ErrorType}: {Error}; reassigning",
                        label, MainnetPeerSession.ParseHost(peer.Enode), ex.GetType().Name, ex.Message);
                }
                finally
                {
                    _inFlight.AddOrUpdate(peer.Id, 0, (_, prev) => Math.Max(0, prev - 1));
                }
            }

            throw new FetchRequestFailedException(
                $"{label} failed after {attempts} attempts across {triedPeers.Count} peers.",
                lastError);
        }

        private async Task<IEthPeer?> ClaimBestPeerAsync(
            HashSet<Guid> excluded, CancellationToken ct)
        {
            // Bumped from 5s to 5 min — when all peers drop simultaneously the
            // dial loop needs time to redial bootnodes / rotate peers in. Earlier
            // 5s deadline was tripping the FetchRequestFailedException path on
            // every transient peer-pool dip and crashing the process.
            var deadline = DateTime.UtcNow.AddMinutes(5);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = SelectBestPeer(excluded);
                if (candidate is not null)
                {
                    _inFlight.AddOrUpdate(candidate.Id, 1, (_, prev) => prev + 1);
                    return candidate;
                }
                try { await Task.Delay(_options.EffectiveNoPeerAvailableBackoff, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return null; }
            }
            return null;
        }

        private void OnPeerRemoved(object? sender, IEthPeer peer)
            => _inFlight.TryRemove(peer.Id, out int _);

        private IEthPeer? SelectBestPeer(HashSet<Guid> excluded)
        {
            IEthPeer? best = null;
            int bestInFlight = int.MaxValue;
            double bestScore = double.NegativeInfinity;

            foreach (var peer in _pool.ActivePeers)
            {
                if (excluded.Contains(peer.Id)) continue;
                var inFlight = _inFlight.TryGetValue(peer.Id, out var n) ? n : 0;
                if (inFlight >= _options.MaxInFlightPerPeer) continue;

                var score = _scoreLookup is not null
                    ? _scoreLookup(peer.Enode).ComputedScore
                    : 0.0;

                if (inFlight < bestInFlight
                    || (inFlight == bestInFlight && score > bestScore))
                {
                    best = peer;
                    bestInFlight = inFlight;
                    bestScore = score;
                }
            }
            return best;
        }
    }

    /// <summary>Thrown when a fetch request fails across all retries.</summary>
    public sealed class FetchRequestFailedException : Exception
    {
        public FetchRequestFailedException(string message, Exception? inner)
            : base(message, inner) { }
    }
}
