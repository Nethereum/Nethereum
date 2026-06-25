using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Peering
{
    /// <summary>
    /// Outbound dial gate with four guarantees:
    /// <list type="number">
    ///   <item>Bounded concurrent dials. At most
    ///     <see cref="DialSchedulerOptions.MaxActiveDials"/> dial attempts in
    ///     flight at once for untrusted candidates; excess
    ///     <see cref="TryReserveSlotAsync"/> callers wait until a slot is
    ///     freed via <see cref="ReleaseSlot"/>.</item>
    ///   <item>Inbound/outbound ratio. At most
    ///     <c>MaxPeers/2 + 1</c> of the steady-state pool may be sourced from
    ///     outbound dials we initiated (counted as live outbound peers plus
    ///     in-flight outbound dials). Caps an attacker who can only convince
    ///     the discovery layer to advertise — they cannot fill more than half
    ///     of the pool without surviving the inbound-listener admission gate.</item>
    ///   <item>Recent-dial history. After every <see cref="ReleaseSlot"/> the
    ///     candidate is suppressed from re-reservation for
    ///     <see cref="DialSchedulerOptions.DialHistoryExpiration"/> (default
    ///     5 minutes). Prevents tight re-dial loops against a peer that just
    ///     disconnected or timed out.</item>
    ///   <item>Trusted bypass. Candidates flagged
    ///     <see cref="DialCandidate.IsTrusted"/> ignore the concurrent-dial
    ///     cap and the ratio cap but still get history suppression on the
    ///     shorter <see cref="DialSchedulerOptions.TrustedHistoryExpiration"/>
    ///     (default 30 seconds).</item>
    /// </list>
    /// The caller (e.g. <c>PeerPoolManager</c>) must invoke
    /// <see cref="ReleaseSlot"/> exactly once for every successful
    /// <see cref="TryReserveSlotAsync"/> (which is why the call sites use a
    /// <c>try/finally</c>). <see cref="OnPeerConnected"/> and
    /// <see cref="OnPeerDisconnected"/> maintain the live inbound/outbound
    /// counters consulted by the ratio cap and may be called independently of
    /// the reserve/release pair (e.g. for inbound peers admitted by the
    /// listener).
    /// </summary>
    public sealed class DialScheduler
    {
        private readonly DialSchedulerOptions _options;
        private readonly Func<DateTimeOffset> _now;
        private readonly SemaphoreSlim _activeDialSlots;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _history =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly object _ratioLock = new object();
        private int _outboundDialsInFlight;
        private int _outboundPeersLive;
        private int _inboundPeersLive;

        /// <summary>
        /// Construct the scheduler. <paramref name="now"/> is overridable so
        /// tests can drive history expiration deterministically; defaults to
        /// <see cref="DateTimeOffset.UtcNow"/>.
        /// </summary>
        public DialScheduler(DialSchedulerOptions options, Func<DateTimeOffset> now = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.MaxActiveDials <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"{nameof(DialSchedulerOptions.MaxActiveDials)} must be positive.");
            if (_options.MaxPeers <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"{nameof(DialSchedulerOptions.MaxPeers)} must be positive.");
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _activeDialSlots = new SemaphoreSlim(_options.MaxActiveDials, _options.MaxActiveDials);
        }

        /// <summary>In-flight outbound dials currently holding a slot.</summary>
        public int ActiveDialCount => Volatile.Read(ref _outboundDialsInFlight);

        /// <summary>Connected peers we initiated the connection to.</summary>
        public int OutboundPeerCount => Volatile.Read(ref _outboundPeersLive);

        /// <summary>Connected peers that initiated the connection to us.</summary>
        public int InboundPeerCount => Volatile.Read(ref _inboundPeersLive);

        /// <summary>
        /// Max number of outbound dials + outbound peers that may exist
        /// concurrently per the <c>MaxPeers/2 + 1</c> rule. Exposed for
        /// observability and tests.
        /// </summary>
        public int OutboundCap => (_options.MaxPeers / 2) + 1;

        /// <summary>
        /// Try to reserve a dial slot for <paramref name="candidate"/>.
        /// Returns <c>true</c> after acquiring a slot — the caller must then
        /// dial, and on completion invoke <see cref="ReleaseSlot"/> exactly
        /// once. Returns <c>false</c> if the candidate is suppressed by
        /// recent-dial history or (for untrusted candidates) the ratio cap,
        /// indicating the caller should try a different candidate. For
        /// untrusted candidates the call <em>may</em> wait on the concurrent
        /// cap before deciding.
        /// </summary>
        public async Task<bool> TryReserveSlotAsync(
            DialCandidate candidate, CancellationToken ct)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));

            PruneExpiredHistory();

            if (IsInHistory(candidate))
                return false;

            if (!candidate.IsTrusted && !CanReserveOutboundSlot())
                return false;

            if (!candidate.IsTrusted)
            {
                await _activeDialSlots.WaitAsync(ct).ConfigureAwait(false);
                // Re-check ratio under lock now that we hold the concurrent
                // slot. Another caller may have grown _outboundDialsInFlight
                // while we waited; if we'd now exceed the cap, release and
                // return false so the caller picks a different candidate.
                if (!TryClaimOutboundUnderRatio())
                {
                    _activeDialSlots.Release();
                    return false;
                }
            }
            // Trusted candidates skip both the concurrent cap and the ratio
            // counter — they are accounted as outbound peers only after
            // OnPeerConnected.

            // History entry written eagerly so a parallel TryReserveSlotAsync
            // for the same key is suppressed during the dial. Overwritten on
            // ReleaseSlot with the trusted/untrusted-tagged timestamp.
            _history[candidate.Key] = _now();
            return true;
        }

        /// <summary>
        /// Release a slot previously acquired by
        /// <see cref="TryReserveSlotAsync"/> and record the candidate in
        /// dial history. The TTL applied is
        /// <see cref="DialSchedulerOptions.TrustedHistoryExpiration"/> for
        /// trusted candidates, otherwise
        /// <see cref="DialSchedulerOptions.DialHistoryExpiration"/>.
        /// Idempotent in the sense that the history overwrite uses the latest
        /// timestamp; calling <see cref="ReleaseSlot"/> twice for the same
        /// reservation will release one slot more than was claimed and
        /// trigger a <see cref="SemaphoreFullException"/>.
        /// </summary>
        public void ReleaseSlot(DialCandidate candidate, DialOutcome outcome)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));
            _ = outcome; // success and failure both record history so a repeat attempt cools down regardless of outcome

            _history[candidate.Key] = _now();

            if (!candidate.IsTrusted)
            {
                Interlocked.Decrement(ref _outboundDialsInFlight);
                _activeDialSlots.Release();
            }
        }

        /// <summary>
        /// Notify the scheduler that a peer has joined the pool. For outbound
        /// connections this increments the live outbound counter; for inbound
        /// it increments the inbound counter. Trusted peers count toward the
        /// live counters too — they bypass the cap at reservation time but
        /// once connected they occupy a slot like any other peer.
        /// </summary>
        public void OnPeerConnected(string key, PeerDirection direction)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (direction == PeerDirection.Outbound)
                Interlocked.Increment(ref _outboundPeersLive);
            else
                Interlocked.Increment(ref _inboundPeersLive);
        }

        /// <summary>
        /// Notify the scheduler that a peer left the pool. Both inbound and
        /// outbound peers funnel through here; <paramref name="direction"/>
        /// must match what was passed to
        /// <see cref="OnPeerConnected(string, PeerDirection)"/> for the same
        /// peer.
        /// </summary>
        public void OnPeerDisconnected(string key, PeerDirection direction)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (direction == PeerDirection.Outbound)
            {
                if (Interlocked.Decrement(ref _outboundPeersLive) < 0)
                    Interlocked.Increment(ref _outboundPeersLive);
            }
            else
            {
                if (Interlocked.Decrement(ref _inboundPeersLive) < 0)
                    Interlocked.Increment(ref _inboundPeersLive);
            }
        }

        private bool IsInHistory(DialCandidate candidate)
        {
            if (!_history.TryGetValue(candidate.Key, out var lastDialed))
                return false;
            var ttl = candidate.IsTrusted
                ? _options.TrustedHistoryExpiration
                : _options.DialHistoryExpiration;
            if (ttl <= TimeSpan.Zero) return false;
            return (_now() - lastDialed) < ttl;
        }

        private bool CanReserveOutboundSlot()
        {
            lock (_ratioLock)
            {
                return _outboundDialsInFlight + _outboundPeersLive < OutboundCap;
            }
        }

        private bool TryClaimOutboundUnderRatio()
        {
            lock (_ratioLock)
            {
                if (_outboundDialsInFlight + _outboundPeersLive >= OutboundCap)
                    return false;
                _outboundDialsInFlight++;
                return true;
            }
        }

        private void PruneExpiredHistory()
        {
            // Lazy prune: dropped entries are guaranteed past both TTLs (we
            // can't tell trusted from untrusted at prune time so we use the
            // longer TTL as the conservative threshold). Bounded sweep keeps
            // the map size proportional to active candidate flux rather than
            // total candidates ever seen.
            if (_history.Count == 0) return;
            var cutoff = _now() - _options.DialHistoryExpiration;
            foreach (var kv in _history)
            {
                if (kv.Value < cutoff)
                    _history.TryRemove(kv.Key, out _);
            }
        }
    }
}
