using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Nethereum.DevP2P.Sync.FullSync;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperPhase1StallRetryTests
    {
        private static Task NoDelay(TimeSpan span, CancellationToken ct) => Task.CompletedTask;

        [Fact]
        public async Task Given_a_persistent_backfill_stall_When_the_hard_threshold_elapses_Then_it_retries_and_rebuilds_the_queue()
        {
            var attempts = new List<int>();
            var expected = new ParallelBlockBackfiller.BackfillResult { Ran = true, EndBlock = 7 };

            Task<ParallelBlockBackfiller.BackfillResult> Attempt(CancellationToken ct)
            {
                attempts.Add(attempts.Count);
                if (attempts.Count < 3)
                    throw new BackfillStalledException(cursor: 5, elapsed: ParallelBlockBackfiller.HardStallThreshold);
                return Task.FromResult(expected);
            }

            var result = await SnapBootstrapper.RunPhase1BackfillWithStallRetryAsync(
                Attempt, NullLogger.Instance, CancellationToken.None, NoDelay);

            Assert.Same(expected, result);
            Assert.Equal(3, attempts.Count);
        }

        [Fact]
        public async Task Given_no_stall_When_the_first_attempt_succeeds_Then_it_is_not_retried()
        {
            var attempts = 0;
            var expected = new ParallelBlockBackfiller.BackfillResult { Ran = true, EndBlock = 3 };

            Task<ParallelBlockBackfiller.BackfillResult> Attempt(CancellationToken ct)
            {
                attempts++;
                return Task.FromResult(expected);
            }

            var result = await SnapBootstrapper.RunPhase1BackfillWithStallRetryAsync(
                Attempt, NullLogger.Instance, CancellationToken.None, NoDelay);

            Assert.Same(expected, result);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task Given_real_cancellation_When_the_attempt_is_cancelled_Then_it_propagates_and_is_not_retried()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var attempts = 0;

            Task<ParallelBlockBackfiller.BackfillResult> Attempt(CancellationToken ct)
            {
                attempts++;
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new ParallelBlockBackfiller.BackfillResult { Ran = true });
            }

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                SnapBootstrapper.RunPhase1BackfillWithStallRetryAsync(
                    Attempt, NullLogger.Instance, cts.Token, NoDelay));

            Assert.Equal(1, attempts);
        }
    }
}
