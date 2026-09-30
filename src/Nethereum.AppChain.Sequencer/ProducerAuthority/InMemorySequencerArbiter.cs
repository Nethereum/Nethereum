using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sequencer.ProducerAuthority
{
    public sealed class InMemorySequencerArbiter : ISequencerArbiter
    {
        private readonly TimeSpan _leaseTtl;
        private readonly Func<DateTimeOffset> _clock;
        private readonly object _lock = new();

        private string? _holderNodeId;
        private long _fencingToken;
        private DateTimeOffset _expiresAtUtc;

        public InMemorySequencerArbiter(TimeSpan leaseTtl, Func<DateTimeOffset>? clock = null)
        {
            if (leaseTtl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseTtl));
            _leaseTtl = leaseTtl;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public Task<LeaseGrant?> TryAcquireOrRenewAsync(string candidateNodeId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(candidateNodeId))
                throw new ArgumentException("A candidate needs a node id.", nameof(candidateNodeId));

            lock (_lock)
            {
                var now = _clock();
                var leaseIsActive = _holderNodeId != null && now < _expiresAtUtc;

                if (leaseIsActive && !string.Equals(_holderNodeId, candidateNodeId, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult<LeaseGrant?>(null);

                if (!leaseIsActive)
                {
                    Interlocked.Increment(ref _fencingToken);
                    _holderNodeId = candidateNodeId;
                }

                _expiresAtUtc = now + _leaseTtl;
                return Task.FromResult<LeaseGrant?>(new LeaseGrant(candidateNodeId, _fencingToken, _expiresAtUtc));
            }
        }

        public Task ReleaseAsync(string nodeId, long fencingToken, CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (string.Equals(_holderNodeId, nodeId, StringComparison.OrdinalIgnoreCase) && _fencingToken == fencingToken)
                {
                    _expiresAtUtc = _clock() - TimeSpan.FromTicks(1);
                }
            }
            return Task.CompletedTask;
        }

        public Task<SequencerStatus> CurrentAsync(CancellationToken ct = default)
        {
            lock (_lock)
            {
                return Task.FromResult(new SequencerStatus(
                    _holderNodeId,
                    _fencingToken,
                    _holderNodeId != null ? _expiresAtUtc : null));
            }
        }

        public void ForceRevoke(string nodeId)
        {
            lock (_lock)
            {
                if (string.Equals(_holderNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    _expiresAtUtc = _clock() - TimeSpan.FromTicks(1);
                }
            }
        }
    }
}
