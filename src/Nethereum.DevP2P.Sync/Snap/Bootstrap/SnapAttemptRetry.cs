using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.Util;
using static Nethereum.DevP2P.Sync.Snap.Bootstrap.SnapSyncOrchestrator;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal enum RetryKind { TasksetStalled, LeaseStalled, Other }

    internal sealed class SnapAttemptRetry
    {
        private readonly ILogger _logger;
        private int _attempt;
        private readonly Backoff _backoff = new Backoff(SnapAttemptInitialBackoffMs);

        internal SnapAttemptRetry(ILogger logger)
        {
            _logger = logger;
        }

        internal void NextAttempt() => _attempt++;

        internal async Task BackOffAsync(RetryKind kind, Exception ex, CancellationToken ct)
        {
            (bool escalated, int cap) = ComputeRetryEscalation(_attempt);
            Log(kind, escalated, ex);
            await Task.Delay(_backoff.Current, ct).ConfigureAwait(false);
            _backoff.Advance(cap);
        }

        private void Log(RetryKind kind, bool escalated, Exception ex)
        {
            switch (kind)
            {
                case RetryKind.TasksetStalled:
                    if (escalated)
                        _logger.LogError(ex,
                            "snap.phase2.recycle reason=taskset_stalled attempt={Attempt}; {Threshold}+ consecutive failures - retrying in {BackoffMs}ms.",
                            _attempt, SnapAttemptEscalationThreshold, _backoff.Current);
                    else
                        _logger.LogWarning(ex,
                            "snap.phase2.recycle reason=taskset_stalled attempt={Attempt}; retrying in {BackoffMs}ms",
                            _attempt, _backoff.Current);
                    break;
                case RetryKind.LeaseStalled:
                    if (escalated)
                        _logger.LogError(ex,
                            "snap.phase2.recycle reason=lease_stalled attempt={Attempt}; {Threshold}+ consecutive failures - retrying in {BackoffMs}ms.",
                            _attempt, SnapAttemptEscalationThreshold, _backoff.Current);
                    else
                        _logger.LogWarning(ex,
                            "snap.phase2.recycle reason=lease_stalled attempt={Attempt}; retrying in {BackoffMs}ms",
                            _attempt, _backoff.Current);
                    break;
                default:
                    if (escalated)
                        _logger.LogError(ex,
                            "snap.bootstrap.retry.stalled attempt={Attempt} reason={Reason}; {Threshold}+ consecutive " +
                            "failures — retrying in {BackoffMs}ms. This node has not reached Phase 4 after multiple " +
                            "full bootstrap attempts; investigate (no genesis fallback).",
                            _attempt, ex.GetType().Name, SnapAttemptEscalationThreshold, _backoff.Current);
                    else
                        _logger.LogWarning(ex,
                            "snap.bootstrap.retry attempt={Attempt} reason={Reason}; retrying in {BackoffMs}ms (no genesis fallback)",
                            _attempt, ex.GetType().Name, _backoff.Current);
                    break;
            }
        }
    }
}
