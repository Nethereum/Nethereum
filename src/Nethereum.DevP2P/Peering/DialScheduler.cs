using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.DevP2P.Peering
{
    public sealed class DialScheduler
    {
        private readonly DialSchedulerOptions _options;
        private readonly int _maxPeers;
        private readonly Func<DateTimeOffset> _now;
        private readonly SemaphoreSlim _activeDialSlots;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _history =
            new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private readonly object _ratioLock = new object();
        private int _outboundDialsInFlight;
        private int _outboundPeersLive;
        private int _inboundPeersLive;

        public DialScheduler(DialSchedulerOptions options, int maxPeers, Func<DateTimeOffset> now = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.MaxActiveDials <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"{nameof(DialSchedulerOptions.MaxActiveDials)} must be positive.");
            if (maxPeers <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maxPeers),
                    $"{nameof(maxPeers)} must be positive.");
            _maxPeers = maxPeers;
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _activeDialSlots = new SemaphoreSlim(_options.MaxActiveDials, _options.MaxActiveDials);
        }

        public int ActiveDialCount => Volatile.Read(ref _outboundDialsInFlight);

        public int OutboundPeerCount => Volatile.Read(ref _outboundPeersLive);

        public int InboundPeerCount => Volatile.Read(ref _inboundPeersLive);

        public int OutboundCap => (_maxPeers / 2) + 1;

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
                if (!TryClaimOutboundUnderRatio())
                {
                    _activeDialSlots.Release();
                    return false;
                }
            }

            _history[candidate.Key] = _now();
            return true;
        }

        public void ReleaseSlot(DialCandidate candidate, DialOutcome outcome)
        {
            if (candidate is null) throw new ArgumentNullException(nameof(candidate));
            _ = outcome;

            _history[candidate.Key] = _now();

            if (!candidate.IsTrusted)
            {
                lock (_ratioLock)
                {
                    _outboundDialsInFlight--;
                }
                _activeDialSlots.Release();
            }
        }

        public void OnPeerConnected(string key, PeerDirection direction)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            if (direction == PeerDirection.Outbound)
                Interlocked.Increment(ref _outboundPeersLive);
            else
                Interlocked.Increment(ref _inboundPeersLive);
        }

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
