using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nethereum.DevP2P.Sync.Scheduling
{
    internal sealed class PeerBoard
    {
        private readonly IPeerPool _pool;
        private readonly FetchRequestSchedulerOptions _options;
        private readonly Func<string, PeerScore>? _scoreLookup;
        private readonly PeerQuarantine _quarantine;
        private readonly ILogger<FetchRequestScheduler> _logger;

        private readonly ConcurrentDictionary<Guid, int> _inFlight = new();
        private readonly object _claimGate = new();

        private readonly ConcurrentDictionary<Guid, double> _latencyEmaMs = new();
        private const double LatencyEmaAlpha = 0.2;
        private const double AdaptiveTimeoutFactor = 5.0;
        private static readonly TimeSpan MinAdaptiveTimeout = TimeSpan.FromSeconds(2);
        private const long TrustedTimeoutMultiplier = 4;

        private const double UnknownPeerLatencyMs = 1_000.0;

        internal PeerBoard(
            IPeerPool pool,
            FetchRequestSchedulerOptions options,
            Func<string, PeerScore>? scoreLookup,
            PeerQuarantine quarantine,
            ILogger<FetchRequestScheduler> logger)
        {
            _pool = pool;
            _options = options;
            _scoreLookup = scoreLookup;
            _quarantine = quarantine;
            _logger = logger;
        }

        internal async Task<IEthPeer?> ClaimAsync(
            HashSet<Guid> excluded, CancellationToken ct, Func<IEthPeer, bool>? peerFilter,
            Func<bool>? noPeerCanServe, byte[]? statelessRoot)
        {
            var deadline = DateTime.UtcNow.AddMinutes(5);
            var nextSpinDiag = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                var candidate = TryClaim(excluded, peerFilter);
                if (candidate is not null) return candidate;
                if (noPeerCanServe != null && noPeerCanServe()) return null;
                if (DateTime.UtcNow >= nextSpinDiag)
                {
                    nextSpinDiag = DateTime.UtcNow.AddSeconds(30);
                    LogClaimSpin(excluded, peerFilter, statelessRoot);
                }
                try { await Task.Delay(_options.EffectiveNoPeerAvailableBackoff, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return null; }
            }
            return null;
        }

        private IEthPeer? TryClaim(HashSet<Guid> excluded, Func<IEthPeer, bool>? peerFilter)
        {
            lock (_claimGate)
            {
                var candidate = SelectBest(excluded, peerFilter);
                if (candidate is not null) _inFlight.AddOrUpdate(candidate.Id, 1, (_, prev) => prev + 1);
                return candidate;
            }
        }

        private void LogClaimSpin(HashSet<Guid> excluded, Func<IEthPeer, bool>? peerFilter, byte[]? statelessRoot)
        {
            int total = 0, excludedCount = 0, filteredOut = 0, quarantined = 0, stateless = 0, atCapacity = 0;
            foreach (var peer in _pool.ActivePeers)
            {
                total++;
                if (excluded.Contains(peer.Id)) { excludedCount++; continue; }
                if (peerFilter is not null && !peerFilter(peer))
                {
                    filteredOut++;
                    if (peer is SyncPeerSession ms && ms.SupportsSnap && _quarantine.IsSnapStateQuarantined(ms.Id))
                        quarantined++;
                    if (statelessRoot != null && _quarantine.IsStateless(peer, statelessRoot))
                        stateless++;
                    continue;
                }
                var inFlight = _inFlight.TryGetValue(peer.Id, out var n) ? n : 0;
                if (inFlight >= _options.MaxInFlightPerPeer) atCapacity++;
            }
            _logger.LogWarning(
                "fetch.claim.spin no eligible peer for 30s+: total={Total} excluded={Excluded} filtered_out={FilteredOut} (quarantined={Quarantined} stateless={Stateless}) at_inflight_cap={AtCapacity} max_inflight={Max}",
                total, excludedCount, filteredOut, quarantined, stateless, atCapacity, _options.MaxInFlightPerPeer);
        }

        internal void Release(Guid peerId)
        {
            _inFlight.AddOrUpdate(peerId, 0, (_, prev) => Math.Max(0, prev - 1));
        }

        internal void Forget(Guid peerId)
        {
            _inFlight.TryRemove(peerId, out int _);
            _latencyEmaMs.TryRemove(peerId, out _);
        }

        internal bool HasCandidate(HashSet<Guid> excluded, Func<IEthPeer, bool>? peerFilter)
        {
            foreach (var peer in _pool.ActivePeers)
                if (!excluded.Contains(peer.Id) && (peerFilter is null || peerFilter(peer))) return true;
            return false;
        }

        internal int InFlightOf(Guid peerId)
            => _inFlight.TryGetValue(peerId, out var n) ? n : 0;

        internal void RecordLatency(Guid id, double ms)
        {
            if (ms <= 0) ms = 0.1;
            _latencyEmaMs.AddOrUpdate(id, ms, (_, prev) => LatencyEmaAlpha * ms + (1 - LatencyEmaAlpha) * prev);
        }

        internal TimeSpan AdaptiveTimeout(Guid id, bool isTrusted = false)
        {
            var max = isTrusted
                ? TimeSpan.FromTicks(_options.EffectivePerRequestTimeout.Ticks * TrustedTimeoutMultiplier)
                : _options.EffectivePerRequestTimeout;
            if (!_latencyEmaMs.TryGetValue(id, out var ema)) return max;
            var floor = Math.Min(MinAdaptiveTimeout.TotalMilliseconds, max.TotalMilliseconds);
            var ms = Math.Clamp(ema * AdaptiveTimeoutFactor, floor, max.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(ms);
        }

        internal double LatencyEma(Guid peerId)
            => _latencyEmaMs.TryGetValue(peerId, out var v) ? v : 0;

        private IEthPeer? SelectBest(HashSet<Guid> excluded, Func<IEthPeer, bool>? peerFilter = null)
        {
            IEthPeer? best = null;
            bool bestTrusted = false;
            int bestInFlight = int.MaxValue;
            double bestLatency = double.MaxValue;
            double bestScore = double.NegativeInfinity;
            bool bestDemoted = false;

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

                var demoted = _quarantine.IsClaimDemoted(peer.Id);

                bool better = demoted != bestDemoted
                    ? !demoted
                    : peer.IsTrusted != bestTrusted
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
                    bestDemoted = demoted;
                }
            }
            return best;
        }
    }
}
