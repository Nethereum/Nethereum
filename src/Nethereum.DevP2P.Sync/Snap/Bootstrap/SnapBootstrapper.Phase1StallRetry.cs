using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.DevP2P.Sync.FullSync;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    public static partial class SnapBootstrapper
    {
        internal static readonly TimeSpan Phase1StallRetryInitialBackoff = TimeSpan.FromSeconds(2);
        internal static readonly TimeSpan Phase1StallRetryMaxBackoff = TimeSpan.FromSeconds(30);

        internal static async Task<ParallelBlockBackfiller.BackfillResult> RunPhase1BackfillWithStallRetryAsync(
            Func<CancellationToken, Task<ParallelBlockBackfiller.BackfillResult>> attempt,
            ILogger logger,
            CancellationToken ct,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            delay ??= Task.Delay;
            var backoff = Phase1StallRetryInitialBackoff;

            while (true)
            {
                try
                {
                    return await attempt(ct).ConfigureAwait(false);
                }
                catch (BackfillStalledException ex)
                {
                    logger.LogWarning(
                        "snap.phase1.stalled {Message} — retrying in {BackoffSeconds:F0}s (rebuilding the backfill queue)",
                        ex.Message, backoff.TotalSeconds);
                    await delay(backoff, ct).ConfigureAwait(false);
                    var doubled = backoff.Ticks * 2;
                    backoff = doubled > Phase1StallRetryMaxBackoff.Ticks
                        ? Phase1StallRetryMaxBackoff
                        : TimeSpan.FromTicks(doubled);
                }
            }
        }
    }
}
