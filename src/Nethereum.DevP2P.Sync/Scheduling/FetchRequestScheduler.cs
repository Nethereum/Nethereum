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
using Nethereum.Model.P2P.Snap;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    public enum TimeoutStreakKind
    {
        Eth,
        Snap
    }

    public sealed class FetchRequestScheduler : IFetchRequestScheduler
    {
        private readonly IPeerPool _pool;
        private readonly IPeerRequestWorker _worker;
        private readonly FetchRequestSchedulerOptions _options;
        private readonly Func<string, PeerScore>? _scoreLookup;
        private readonly ILogger<FetchRequestScheduler> _logger;

        private readonly ConcurrentDictionary<Guid, int> _inFlight = new();

        private readonly ConcurrentDictionary<Guid, double> _latencyEmaMs = new();
        private const double LatencyEmaAlpha = 0.2;
        private const double AdaptiveTimeoutFactor = 5.0;
        private static readonly TimeSpan MinAdaptiveTimeout = TimeSpan.FromSeconds(2);
        private const long TrustedTimeoutMultiplier = 4;

        private const double UnknownPeerLatencyMs = 1_000.0;

        private readonly ConcurrentDictionary<(Guid PeerId, TimeoutStreakKind Kind), int> _consecutiveTimeouts = new();
        private const int RepeatedTimeoutQuarantineThreshold = 3;

        private const int TrustedRepeatedTimeoutQuarantineThreshold = 20;

        public bool RegisterTimeoutForQuarantine(Guid peerId, TimeoutStreakKind kind, bool isTrusted, out int triggeredAtCount)
        {
            var key = (peerId, kind);
            var count = _consecutiveTimeouts.AddOrUpdate(key, 1, (_, prev) => prev + 1);
            triggeredAtCount = count;
            var threshold = isTrusted ? TrustedRepeatedTimeoutQuarantineThreshold : RepeatedTimeoutQuarantineThreshold;
            if (count < threshold) return false;
            _consecutiveTimeouts[key] = 0;
            return true;
        }

        public void RegisterSuccessForQuarantine(Guid peerId, TimeoutStreakKind kind)
        {
            _consecutiveTimeouts.TryRemove((peerId, kind), out _);
            if (kind == TimeoutStreakKind.Snap) _snapQuarantineCycles.TryRemove(peerId, out _);
        }

        private void DropOnRepeatedEthTimeout(IEthPeer peer)
        {
            _logger.LogWarning(
                "eth.peer.timeout.drop peer={Host} — repeated eth timeout, dropping for redial",
                SyncPeerSession.ParseHost(peer.Enode));
            _ = _pool.DropAsync(peer.Id, "repeated eth timeout", CancellationToken.None);
        }

        public int GetConsecutiveTimeoutsForTest(Guid peerId, TimeoutStreakKind kind)
            => _consecutiveTimeouts.TryGetValue((peerId, kind), out var n) ? n : 0;

        public int GetSnapQuarantineCyclesForTest(Guid peerId)
            => _snapQuarantineCycles.TryGetValue(peerId, out var n) ? n : 0;

        public TimeSpan GetAdaptiveTimeoutForTest(Guid peerId, bool isTrusted)
            => AdaptiveTimeout(peerId, isTrusted);

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
            ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"headers {startBlock}+{limit}{(reverse ? " rev" : string.Empty)}",
                (peer, attemptCt) => _worker.GetHeadersAsync(peer, startBlock, limit, reverse, attemptCt),
                initialExcluded: null,
                ct,
                peerFilter: HeaderServingFilter,
                onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
            return result;
        }

        public async Task<(List<BlockHeader> Headers, Guid PeerId)> FetchHeadersWithPeerAsync(
            ulong startBlock, ulong limit, CancellationToken ct, bool reverse = false)
        {
            var (result, peerId) = await ExecuteWithRetryAsync(
                $"headers {startBlock}+{limit}{(reverse ? " rev" : string.Empty)}",
                (peer, attemptCt) => _worker.GetHeadersAsync(peer, startBlock, limit, reverse, attemptCt),
                initialExcluded: null,
                ct,
                peerFilter: HeaderServingFilter,
                onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
            return (result, peerId);
        }

        public async Task<List<BlockHeader>> FetchHeadersByHashAsync(
            byte[] startHash, ulong limit, CancellationToken ct)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"headers-by-hash+{limit}",
                (peer, attemptCt) => _worker.GetHeadersByHashAsync(peer, startHash, limit, attemptCt),
                initialExcluded: null,
                ct,
                onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
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

            if (blockHashes.Count <= chunkSize || maxParallel <= 1)
            {
                var (single, singlePeer) = await ExecuteWithRetryAsync(
                    $"bodies x{blockHashes.Count}",
                    (peer, attemptCt) => _worker.GetBodiesAsync(peer, blockHashes, attemptCt),
                    excludePeers,
                    ct,
                    onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
                return new BodyFetchResult(single, new[] { singlePeer });
            }

            var activePeerCount = _pool.ActivePeers.Count;
            if (activePeerCount <= 1)
            {
                var (single, singlePeer) = await ExecuteWithRetryAsync(
                    $"bodies x{blockHashes.Count}",
                    (peer, attemptCt) => _worker.GetBodiesAsync(peer, blockHashes, attemptCt),
                    excludePeers,
                    ct,
                    onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
                return new BodyFetchResult(single, new[] { singlePeer });
            }

            var effectiveParallel = Math.Min(maxParallel, activePeerCount);
            var chunks = SplitIntoChunks(blockHashes, chunkSize, effectiveParallel);

            var chunkTasks = new Task<(List<BlockBody> Bodies, Guid PeerId)>[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                chunkTasks[i] = ExecuteWithRetryAsync(
                    $"bodies x{chunk.Count}",
                    (peer, attemptCt) => _worker.GetBodiesAsync(peer, chunk, attemptCt),
                    excludePeers,
                    ct,
                    onRepeatedTimeout: DropOnRepeatedEthTimeout,
                    kind: TimeoutStreakKind.Eth);
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

        public async Task<List<List<Receipt>>> FetchReceiptsAsync(
            IReadOnlyList<byte[]> blockHashes, CancellationToken ct)
        {
            if (blockHashes is null) throw new ArgumentNullException(nameof(blockHashes));
            if (blockHashes.Count == 0) return new List<List<Receipt>>();

            var chunkSize = _options.EffectiveReceiptFetchChunkSize;
            var maxParallel = _options.EffectiveMaxParallelReceiptFetches;

            if (blockHashes.Count <= chunkSize || maxParallel <= 1)
            {
                var (single, _) = await ExecuteWithRetryAsync(
                    $"receipts x{blockHashes.Count}",
                    (peer, attemptCt) => _worker.GetReceiptsAsync(peer, blockHashes, attemptCt),
                    initialExcluded: null,
                    ct,
                    onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
                return single;
            }

            var activePeerCount = _pool.ActivePeers.Count;
            if (activePeerCount <= 1)
            {
                var (single, _) = await ExecuteWithRetryAsync(
                    $"receipts x{blockHashes.Count}",
                    (peer, attemptCt) => _worker.GetReceiptsAsync(peer, blockHashes, attemptCt),
                    initialExcluded: null,
                    ct,
                    onRepeatedTimeout: DropOnRepeatedEthTimeout,
                kind: TimeoutStreakKind.Eth).ConfigureAwait(false);
                return single;
            }

            var effectiveParallel = Math.Min(maxParallel, activePeerCount);
            var chunks = SplitIntoChunks(blockHashes, chunkSize, effectiveParallel);

            var chunkTasks = new Task<(List<List<Receipt>>, Guid)>[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                chunkTasks[i] = ExecuteWithRetryAsync(
                    $"receipts x{chunk.Count}",
                    (peer, attemptCt) => _worker.GetReceiptsAsync(peer, chunk, attemptCt),
                    initialExcluded: null,
                    ct,
                    onRepeatedTimeout: DropOnRepeatedEthTimeout,
                    kind: TimeoutStreakKind.Eth);
            }

            var results = await Task.WhenAll(chunkTasks).ConfigureAwait(false);
            var merged = new List<List<Receipt>>(blockHashes.Count);
            foreach (var part in results)
            {
                if (part.Item1 is not null) merged.AddRange(part.Item1);
            }
            return merged;
        }

        private static List<IReadOnlyList<byte[]>> SplitIntoChunks(
            IReadOnlyList<byte[]> hashes, int chunkSize, int maxChunks)
        {
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
            CancellationToken ct,
            Func<IEthPeer, bool>? peerFilter = null,
            Action<IEthPeer>? onRepeatedTimeout = null,
            TimeoutStreakKind kind = TimeoutStreakKind.Eth)
        {
            var attempts = 0;
            Exception? lastError = null;
            var triedPeers = initialExcluded is null
                ? new HashSet<Guid>()
                : new HashSet<Guid>(initialExcluded);

            while (attempts < _options.MaxRetriesPerRequest)
            {
                ct.ThrowIfCancellationRequested();
                var peer = await ClaimBestPeerAsync(triedPeers, ct, peerFilter).ConfigureAwait(false);
                if (peer is null)
                {
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

                var counted = true;
                triedPeers.Add(peer.Id);
                attempts++;
                try
                {
                    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    attemptCts.CancelAfter(AdaptiveTimeout(peer.Id, peer.IsTrusted));
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var result = await sendOnce(peer, attemptCts.Token).ConfigureAwait(false);
                    RecordLatency(peer.Id, sw.Elapsed.TotalMilliseconds);
                    if (onRepeatedTimeout != null) RegisterSuccessForQuarantine(peer.Id, kind);
                    _pool.ReportSuccess(peer.Id);
                    return (result, peer.Id);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    lastError = new TimeoutException(
                        $"{label}: peer {SyncPeerSession.ParseHost(peer.Enode)} timed out");
                    _logger.LogWarning(
                        "snap.peer.timeout request={Request} peer={Host} attempt={Attempt}; reassigning",
                        label, SyncPeerSession.ParseHost(peer.Enode), attempts);

                    if (onRepeatedTimeout != null && RegisterTimeoutForQuarantine(peer.Id, kind, peer.IsTrusted, out var timeoutCount))
                    {
                        _logger.LogWarning(
                            "snap.peer.timeout.quarantine request={Request} peer={Host} consecutive={Count} — peer never responds, quarantining",
                            label, SyncPeerSession.ParseHost(peer.Enode), timeoutCount);
                        onRepeatedTimeout(peer);
                    }
                }
                catch (TimeoutException tex)
                {
                    lastError = tex;
                    _logger.LogWarning(
                        "snap.peer.timeout.hard request={Request} peer={Host} attempt={Attempt}; reassigning",
                        label, SyncPeerSession.ParseHost(peer.Enode), attempts);

                    if (onRepeatedTimeout != null && RegisterTimeoutForQuarantine(peer.Id, kind, peer.IsTrusted, out var hardTimeoutCount))
                    {
                        _logger.LogWarning(
                            "snap.peer.timeout.quarantine request={Request} peer={Host} consecutive={Count} — peer never responds, quarantining",
                            label, SyncPeerSession.ParseHost(peer.Enode), hardTimeoutCount);
                        onRepeatedTimeout(peer);
                    }
                }
                catch (SnapPeerCapabilityMismatchException ex)
                {
                    _logger.LogError(
                        "snap.peer.capability_mismatch request={Request} peer={Host} error={Error}; permanent — failing fast without reassigning",
                        label, SyncPeerSession.ParseHost(peer.Enode), ex.Message);
                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                    _logger.LogWarning(
                        "snap.peer.error request={Request} peer={Host} error={ErrorType}: {Error}; reassigning",
                        label, SyncPeerSession.ParseHost(peer.Enode), ex.GetType().Name, ex.Message);
                }
                finally
                {
                    if (counted)
                    {
                        _inFlight.AddOrUpdate(peer.Id, 0, (_, prev) => Math.Max(0, prev - 1));
                    }
                }
            }

            throw new FetchRequestFailedException(
                $"{label} failed after {attempts} attempts across {triedPeers.Count} peers.",
                lastError);
        }

        private async Task<IEthPeer?> ClaimBestPeerAsync(
            HashSet<Guid> excluded, CancellationToken ct, Func<IEthPeer, bool>? peerFilter = null)
        {
            var deadline = DateTime.UtcNow.AddMinutes(5);
            var nextSpinDiag = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = SelectBestPeer(excluded, peerFilter);
                if (candidate is not null)
                {
                    _inFlight.AddOrUpdate(candidate.Id, 1, (_, prev) => prev + 1);
                    return candidate;
                }
                if (DateTime.UtcNow >= nextSpinDiag)
                {
                    nextSpinDiag = DateTime.UtcNow.AddSeconds(30);
                    LogClaimSpin(excluded, peerFilter);
                }
                try { await Task.Delay(_options.EffectiveNoPeerAvailableBackoff, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return null; }
            }
            return null;
        }

        private void LogClaimSpin(HashSet<Guid> excluded, Func<IEthPeer, bool>? peerFilter)
        {
            int total = 0, excludedCount = 0, filteredOut = 0, quarantined = 0, atCapacity = 0;
            foreach (var peer in _pool.ActivePeers)
            {
                total++;
                if (excluded.Contains(peer.Id)) { excludedCount++; continue; }
                if (peerFilter is not null && !peerFilter(peer))
                {
                    filteredOut++;
                    if (peer is SyncPeerSession ms && ms.SupportsSnap && IsSnapStateQuarantined(ms.Id))
                        quarantined++;
                    continue;
                }
                var inFlight = _inFlight.TryGetValue(peer.Id, out var n) ? n : 0;
                if (inFlight >= _options.MaxInFlightPerPeer) atCapacity++;
            }
            _logger.LogWarning(
                "fetch.claim.spin no eligible peer for 30s+: total={Total} excluded={Excluded} filtered_out={FilteredOut} (quarantined={Quarantined}) at_inflight_cap={AtCapacity} max_inflight={Max}",
                total, excludedCount, filteredOut, quarantined, atCapacity, _options.MaxInFlightPerPeer);
        }

        private void OnPeerRemoved(object? sender, IEthPeer peer)
        {
            _inFlight.TryRemove(peer.Id, out int _);
            _latencyEmaMs.TryRemove(peer.Id, out _);
            _headerQuarantineUntilTicks.TryRemove(peer.Id, out _);
            _consecutiveTimeouts.TryRemove((peer.Id, TimeoutStreakKind.Eth), out _);
            _consecutiveTimeouts.TryRemove((peer.Id, TimeoutStreakKind.Snap), out _);
            _snapQuarantineCycles.TryRemove(peer.Id, out _);
        }

        public int GetInFlightCountForTest(Guid peerId)
            => _inFlight.TryGetValue(peerId, out var n) ? n : 0;

        private void RecordLatency(Guid id, double ms)
        {
            if (ms <= 0) ms = 0.1;
            _latencyEmaMs.AddOrUpdate(id, ms, (_, prev) => LatencyEmaAlpha * ms + (1 - LatencyEmaAlpha) * prev);
        }

        private TimeSpan AdaptiveTimeout(Guid id, bool isTrusted = false)
        {
            var max = isTrusted
                ? TimeSpan.FromTicks(_options.EffectivePerRequestTimeout.Ticks * TrustedTimeoutMultiplier)
                : _options.EffectivePerRequestTimeout;
            if (!_latencyEmaMs.TryGetValue(id, out var ema)) return max;
            var ms = Math.Clamp(ema * AdaptiveTimeoutFactor, MinAdaptiveTimeout.TotalMilliseconds, max.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(ms);
        }

        public double GetLatencyEmaForTest(Guid peerId)
            => _latencyEmaMs.TryGetValue(peerId, out var v) ? v : 0;

        public TimeSpan GetAdaptiveTimeoutForTest(Guid peerId) => AdaptiveTimeout(peerId);

        private IEthPeer? SelectBestPeer(HashSet<Guid> excluded, Func<IEthPeer, bool>? peerFilter = null)
        {
            IEthPeer? best = null;
            bool bestTrusted = false;
            int bestInFlight = int.MaxValue;
            double bestLatency = double.MaxValue;
            double bestScore = double.NegativeInfinity;

            foreach (var peer in _pool.ActivePeers)
            {
                if (excluded.Contains(peer.Id)) continue;
                if (peerFilter is not null && !peerFilter(peer)) continue;
                var inFlight = _inFlight.TryGetValue(peer.Id, out var n) ? n : 0;
                if (inFlight >= _options.MaxInFlightPerPeer) continue;

                var score = _scoreLookup is not null
                    ? _scoreLookup(peer.Enode).ComputedScore
                    : 0.0;
                var latency = _latencyEmaMs.TryGetValue(peer.Id, out var l) ? l : UnknownPeerLatencyMs;

                bool better = peer.IsTrusted != bestTrusted
                    ? peer.IsTrusted
                    : inFlight != bestInFlight ? inFlight < bestInFlight
                    : latency != bestLatency ? latency < bestLatency
                    : score > bestScore;

                if (best is null || better)
                {
                    best = peer;
                    bestTrusted = peer.IsTrusted;
                    bestInFlight = inFlight;
                    bestLatency = latency;
                    bestScore = score;
                }
            }
            return best;
        }

        private readonly ConcurrentDictionary<Guid, long> _snapStateQuarantineUntilTicks = new();
        private static readonly TimeSpan SnapStateQuarantineDuration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan TrustedSnapStateCooldown = TimeSpan.FromSeconds(20);

        private static readonly TimeSpan BehindSnapStateCooldown = TimeSpan.FromSeconds(6);
        private const ulong SnapStateBehindMargin = 4;

        private ulong NetworkHeadBlock() => _pool.ActivePeers
            .OfType<SyncPeerSession>()
            .Select(s => s.PeerLatestBlock)
            .DefaultIfEmpty(0UL)
            .Max();

        public static TimeSpan ChooseSnapStateCooldown(ulong peerLatestBlock, ulong networkHeadBlock, bool isTrusted)
        {
            bool behind = networkHeadBlock > SnapStateBehindMargin
                          && peerLatestBlock + SnapStateBehindMargin < networkHeadBlock;
            if (behind) return BehindSnapStateCooldown;
            return isTrusted ? TrustedSnapStateCooldown : SnapStateQuarantineDuration;
        }

        private readonly ConcurrentDictionary<Guid, long> _headerQuarantineUntilTicks = new();
        public TimeSpan HeaderBatchQuarantineDuration = TimeSpan.FromMinutes(2);

        public void QuarantineHeaderPeer(Guid peerId) =>
            _headerQuarantineUntilTicks[peerId] = DateTime.UtcNow.Add(HeaderBatchQuarantineDuration).Ticks;

        public bool IsHeaderPeerQuarantined(Guid peerId) =>
            _headerQuarantineUntilTicks.TryGetValue(peerId, out var untilTicks)
            && DateTime.UtcNow.Ticks < untilTicks;

        private bool HeaderServingFilter(IEthPeer p) => !IsHeaderPeerQuarantined(p.Id);

        private readonly ConcurrentDictionary<Guid, int> _snapQuarantineCycles = new();
        private const int SnapQuarantineDropThreshold = 3;
        private const int TrustedSnapQuarantineDropThreshold = 20;

        public void QuarantineSnapState(IEthPeer peer)
        {
            _snapStateQuarantineUntilTicks[peer.Id] = DateTime.UtcNow
                .Add(ChooseSnapStateCooldown(peer.PeerLatestBlock, NetworkHeadBlock(), peer.IsTrusted)).Ticks;

            var threshold = peer.IsTrusted ? TrustedSnapQuarantineDropThreshold : SnapQuarantineDropThreshold;
            var cycles = _snapQuarantineCycles.AddOrUpdate(peer.Id, 1, (_, prev) => prev + 1);
            if (cycles < threshold) return;

            _snapQuarantineCycles.TryRemove(peer.Id, out _);
            _logger.LogWarning(
                "snap.peer.quarantine.drop peer={Host} — repeated snap quarantine with no intervening success, dropping for redial",
                SyncPeerSession.ParseHost(peer.Enode));
            _ = _pool.DropAsync(peer.Id, "repeated snap quarantine", CancellationToken.None);
        }

        public bool IsSnapStateQuarantined(Guid peerId) =>
            _snapStateQuarantineUntilTicks.TryGetValue(peerId, out var untilTicks)
            && DateTime.UtcNow.Ticks < untilTicks;

        public void OnTargetRootChanged()
        {
            if (_snapStateQuarantineUntilTicks.IsEmpty) return;
            var benched = _snapStateQuarantineUntilTicks.Count;
            _snapStateQuarantineUntilTicks.Clear();
            _logger.LogInformation(
                "snap.quarantine.cleared count={Count} — target root rotated; benched peers re-evaluated against the new root",
                benched);
        }

        private bool SnapStateServingFilter(IEthPeer p) =>
            p is SyncPeerSession ms && ms.SupportsSnap && !IsSnapStateQuarantined(ms.Id);

        public bool IsSnapStateServing(IEthPeer peer) => SnapStateServingFilter(peer);

        public Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
            => FetchAccountRangeAsync(stateRoot, startingHash, limitHash, responseBytes, verifyResponse: null, ct);

        public async Task<AccountRangeMessage> FetchAccountRangeAsync(
            byte[] stateRoot, byte[] startingHash, byte[] limitHash,
            ulong responseBytes, Func<AccountRangeMessage, bool> verifyResponse, CancellationToken ct)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"snap account-range root=0x{HexShort(stateRoot)} start=0x{HexShort(startingHash)}",
                async (peer, attemptCt) =>
                {
                    var resp = await _worker.GetAccountRangeAsync(
                        peer, stateRoot, startingHash, limitHash, responseBytes, attemptCt)
                        .ConfigureAwait(false);

                    var hasAccounts = resp?.Accounts != null && resp.Accounts.Count > 0;
                    var hasProof = resp?.Proof != null && resp.Proof.Count > 0;
                    if (!hasAccounts && !hasProof)
                    {
                        QuarantineSnapState(peer);
                        throw new InvalidOperationException(
                            $"snap account-range root=0x{HexShort(stateRoot)} start=0x{HexShort(startingHash)}: " +
                            "peer returned empty accounts AND empty proof (cannot serve this root — quarantined)");
                    }

                    if (verifyResponse != null && !RunVerify(verifyResponse, resp, "account-range", peer))
                    {
                        QuarantineSnapState(peer);
                        throw new InvalidOperationException(
                            $"snap account-range root=0x{HexShort(stateRoot)} start=0x{HexShort(startingHash)}: " +
                            "peer returned a proof-INVALID response (tampered/malformed — quarantined)");
                    }
                    return resp;
                },
                initialExcluded: null,
                ct,
                peerFilter: SnapStateServingFilter,
                onRepeatedTimeout: QuarantineSnapState,
                kind: TimeoutStreakKind.Snap).ConfigureAwait(false);
            return result;
        }

        public Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, CancellationToken ct)
            => FetchStorageRangesAsync(stateRoot, accountHashes, startingHash, limitHash, responseBytes, verifyResponse: null, ct);

        public async Task<StorageRangesMessage> FetchStorageRangesAsync(
            byte[] stateRoot, List<byte[]> accountHashes,
            byte[] startingHash, byte[] limitHash,
            ulong responseBytes, Func<StorageRangesMessage, bool> verifyResponse, CancellationToken ct)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"snap storage-range root=0x{HexShort(stateRoot)} acct=0x{(accountHashes.Count > 0 ? HexShort(accountHashes[0]) : "")} accounts={accountHashes.Count} start=0x{HexShort(startingHash)}",
                async (peer, attemptCt) =>
                {
                    var resp = await _worker.GetStorageRangesAsync(
                        peer, stateRoot, accountHashes, startingHash, limitHash, responseBytes, attemptCt)
                        .ConfigureAwait(false);

                    var hasSlots = resp?.Slots != null && resp.Slots.Count > 0;
                    var hasProof = resp?.Proof != null && resp.Proof.Count > 0;
                    if (!hasSlots && !hasProof)
                    {
                        QuarantineSnapState(peer);
                        throw new InvalidOperationException(
                            $"snap storage-range root=0x{HexShort(stateRoot)} accounts={accountHashes.Count}: " +
                            "peer returned empty slots AND empty proof (cannot serve this root — quarantined)");
                    }

                    if (verifyResponse != null && !RunVerify(verifyResponse, resp, "storage-range", peer))
                    {
                        QuarantineSnapState(peer);
                        throw new InvalidOperationException(
                            $"snap storage-range root=0x{HexShort(stateRoot)} accounts={accountHashes.Count}: " +
                            "peer returned a proof-INVALID response (tampered/malformed — quarantined)");
                    }
                    return resp;
                },
                initialExcluded: null,
                ct,
                peerFilter: SnapStateServingFilter,
                onRepeatedTimeout: QuarantineSnapState,
                kind: TimeoutStreakKind.Snap).ConfigureAwait(false);
            return result;
        }

        public Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, CancellationToken ct)
            => FetchByteCodesAsync(codeHashes, responseBytes, verifyResponse: null, ct);

        public async Task<ByteCodesMessage> FetchByteCodesAsync(
            List<byte[]> codeHashes, ulong responseBytes, Func<ByteCodesMessage, bool> verifyResponse, CancellationToken ct)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"snap bytecodes x{codeHashes.Count}",
                async (peer, attemptCt) =>
                {
                    var resp = await _worker.GetByteCodesAsync(peer, codeHashes, responseBytes, attemptCt)
                        .ConfigureAwait(false);

                    if (verifyResponse != null && !RunVerify(verifyResponse, resp, "bytecodes", peer))
                    {
                        QuarantineSnapState(peer);
                        throw new InvalidOperationException(
                            $"snap bytecodes x{codeHashes.Count}: peer returned a code whose keccak matches no " +
                            "requested hash (unrequested/over-length — quarantined)");
                    }
                    return resp;
                },
                initialExcluded: null,
                ct,
                peerFilter: SnapStateServingFilter,
                onRepeatedTimeout: QuarantineSnapState,
                kind: TimeoutStreakKind.Snap).ConfigureAwait(false);
            return result;
        }

        public Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, CancellationToken ct)
            => FetchTrieNodesAsync(stateRoot, paths, responseBytes, verifyResponse: null, ct);

        public async Task<TrieNodesMessage> FetchTrieNodesAsync(
            byte[] stateRoot, List<List<byte[]>> paths,
            ulong responseBytes, Func<TrieNodesMessage, bool> verifyResponse, CancellationToken ct)
        {
            var (result, _) = await ExecuteWithRetryAsync(
                $"snap trie-nodes root=0x{HexShort(stateRoot)} paths={paths.Count}",
                async (peer, attemptCt) =>
                {
                    var resp = await _worker.GetTrieNodesAsync(peer, stateRoot, paths, responseBytes, attemptCt)
                        .ConfigureAwait(false);

                    if (verifyResponse != null && !RunVerify(verifyResponse, resp, "trie-nodes", peer))
                    {
                        QuarantineSnapState(peer);
                        throw new InvalidOperationException(
                            $"snap trie-nodes paths={paths.Count}: peer returned a node whose keccak matches no " +
                            "requested hash (unrequested/over-length — quarantined)");
                    }
                    return resp;
                },
                initialExcluded: null,
                ct,
                peerFilter: SnapStateServingFilter,
                onRepeatedTimeout: QuarantineSnapState,
                kind: TimeoutStreakKind.Snap).ConfigureAwait(false);
            return result;
        }

        private bool RunVerify<TMsg>(Func<TMsg, bool> verify, TMsg resp, string kind, IEthPeer peer)
        {
            try
            {
                return verify(resp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "snap.peer.verify.faulted request={Kind} peer={Host} error={ErrorType}: {Error} — treating as verification failure (quarantine+rotate)",
                    kind, SyncPeerSession.ParseHost(peer.Enode), ex.GetType().Name, ex.Message);
                return false;
            }
        }

        private static string HexShort(byte[] hash)
        {
            if (hash == null || hash.Length == 0) return "(empty)";
            var n = System.Math.Min(8, hash.Length);
            var chars = new char[n * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < n; i++)
            {
                chars[i * 2] = hex[hash[i] >> 4];
                chars[i * 2 + 1] = hex[hash[i] & 0x0f];
            }
            return new string(chars);
        }
    }

    public sealed class FetchRequestFailedException : Exception
    {
        public FetchRequestFailedException(string message, Exception? inner)
            : base(message, inner) { }
    }
}
