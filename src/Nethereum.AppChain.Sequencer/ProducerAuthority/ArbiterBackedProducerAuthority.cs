using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.AppChain.Sequencer.Metrics;

namespace Nethereum.AppChain.Sequencer.ProducerAuthority
{
    public sealed class ArbiterBackedProducerAuthority : IProducerAuthority, IAsyncDisposable
    {
        private readonly ISequencerArbiter _arbiter;
        private readonly string _nodeId;
        private readonly TimeSpan _renewInterval;
        private readonly Func<DateTimeOffset> _clock;
        private readonly HAMetrics? _metrics;
        private readonly ILogger<ArbiterBackedProducerAuthority>? _logger;

        private readonly object _lock = new();
        private LeaseGrant? _cachedGrant;

        private readonly CancellationTokenSource? _loopCts;
        private readonly Task? _renewalLoop;

        public ArbiterBackedProducerAuthority(
            ISequencerArbiter arbiter,
            string nodeId,
            TimeSpan renewInterval,
            HAMetrics? metrics = null,
            Func<DateTimeOffset>? clock = null,
            bool startBackgroundLoop = true,
            ILogger<ArbiterBackedProducerAuthority>? logger = null)
        {
            _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
            _nodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            if (renewInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(renewInterval));
            _renewInterval = renewInterval;
            _metrics = metrics;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            _logger = logger;

            if (startBackgroundLoop)
            {
                _loopCts = new CancellationTokenSource();
                _renewalLoop = RunRenewalLoopAsync(_loopCts.Token);
            }
        }

        public ISequencerArbiter Arbiter => _arbiter;

        public string? CurrentProducer()
        {
            lock (_lock)
            {
                if (_cachedGrant == null) return null;
                return _clock() < _cachedGrant.ExpiresAtUtc ? _nodeId : null;
            }
        }

        public long? LastKnownFencingToken
        {
            get
            {
                lock (_lock)
                {
                    return _cachedGrant?.FencingToken;
                }
            }
        }

        public async Task<bool> PollOnceAsync(CancellationToken ct = default)
        {
            var previouslyHeld = HoldsAnUnexpiredLease();

            LeaseGrant? grant;
            try
            {
                grant = await _arbiter.TryAcquireOrRenewAsync(_nodeId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "TryAcquireOrRenewAsync failed for {NodeId}; keeping the last known lease until it locally expires",
                    _nodeId);
                _metrics?.RecordTakeoverAttempt(false);
                return HoldsAnUnexpiredLease();
            }

            lock (_lock)
            {
                if (grant != null)
                {
                    _cachedGrant = grant;
                }
                else
                {
                    _cachedGrant = null;
                }
            }

            _metrics?.RecordTakeoverAttempt(grant != null);
            if (previouslyHeld && grant == null)
            {
                _metrics?.RecordFailover();
            }

            return grant != null;
        }

        private bool HoldsAnUnexpiredLease()
        {
            lock (_lock)
            {
                return _cachedGrant != null && _clock() < _cachedGrant.ExpiresAtUtc;
            }
        }

        private async Task RunRenewalLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Delay(_renewInterval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_loopCts == null) return;

            _loopCts.Cancel();
            if (_renewalLoop != null)
            {
                try { await _renewalLoop.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _loopCts.Dispose();
        }
    }
}
