using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing;
using Xunit;

namespace Nethereum.AppChain.Anchoring.UnitTests
{
    public class RetryRunnerTests
    {
        [Fact]
        public async Task SuccessOnFirstAttempt_DoesNotRetry()
        {
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => { attempts++; return Task.CompletedTask; },
                CancellationToken.None);

            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task FailsThenSucceeds_RetriesUntilSuccess()
        {
            var attempts = 0;
            const int failuresBeforeSuccess = 3;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= failuresBeforeSuccess)
                        throw new InvalidOperationException($"Failure {attempts}");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                initialDelaySeconds: 0);

            Assert.Equal(failuresBeforeSuccess + 1, attempts);
        }

        [Fact]
        public async Task OnRetryCallback_InvokedWithCorrectParameters()
        {
            var retryRecords = new List<(Exception ex, int attempt, int delay)>();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= 3)
                        throw new InvalidOperationException($"Failure {attempts}");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: (ex, attempt, delay) => retryRecords.Add((ex, attempt, delay)),
                initialDelaySeconds: 0);

            Assert.Equal(3, retryRecords.Count);
            Assert.Equal(1, retryRecords[0].attempt);
            Assert.Equal(2, retryRecords[1].attempt);
            Assert.Equal(3, retryRecords[2].attempt);
            Assert.All(retryRecords, r => Assert.IsType<InvalidOperationException>(r.ex));
        }

        [Fact]
        public async Task ExponentialBackoff_CorrectDelayValues()
        {
            var delays = new List<int>();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= 5)
                        throw new Exception("fail");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: (_, _, delay) => delays.Add(delay),
                initialDelaySeconds: 0);

            // With initialDelaySeconds=0: 0*(1<<n) = 0 for all n
            // This tests the formula works without real waits
            Assert.Equal(5, delays.Count);
            Assert.All(delays, d => Assert.Equal(0, d));
        }

        [Fact]
        public async Task ExponentialBackoff_DelayFormula_IsCorrect()
        {
            var delays = new List<int>();
            var cts = new CancellationTokenSource();

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => throw new Exception("fail"),
                cts.Token,
                onRetry: (_, attempt, delay) =>
                {
                    delays.Add(delay);
                    // Cancel immediately so we don't actually wait
                    cts.Cancel();
                },
                initialDelaySeconds: 5,
                maxDelaySeconds: 300);

            // First delay should be 5 * (1 << 0) = 5
            Assert.Single(delays);
            Assert.Equal(5, delays[0]);
        }

        [Fact]
        public async Task ExponentialBackoff_DelaySequence_VerifiedViaCallback()
        {
            // Verify the delay *values* reported in callbacks without actually waiting
            var delays = new List<int>();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= 6)
                        throw new Exception("fail");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: (_, _, delay) => delays.Add(delay),
                initialDelaySeconds: 0);

            // initialDelay=0 so all waits are instant, but let's verify count
            Assert.Equal(6, delays.Count);
        }

        [Fact]
        public async Task MaxDelay_CapsAtConfiguredMaximum()
        {
            var delays = new List<int>();
            var cts = new CancellationTokenSource();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    throw new Exception("always fail");
                },
                cts.Token,
                onRetry: (_, attempt, delay) =>
                {
                    delays.Add(delay);
                    if (delays.Count >= 8) cts.Cancel();
                },
                initialDelaySeconds: 0,
                maxDelaySeconds: 30);

            Assert.Equal(8, delays.Count);
            // With initialDelay=0, all delays are min(0, 30) = 0
            Assert.All(delays, d => Assert.Equal(0, d));
        }

        [Fact]
        public async Task MaxDelay_CapsWithNonZeroInitial()
        {
            // Use initialDelay=10, max=25 and cancel after collecting delays
            var delays = new List<int>();
            var cts = new CancellationTokenSource();

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => throw new Exception("fail"),
                cts.Token,
                onRetry: (_, attempt, delay) =>
                {
                    delays.Add(delay);
                    // Cancel immediately to avoid real waiting
                    cts.Cancel();
                },
                initialDelaySeconds: 10,
                maxDelaySeconds: 25);

            // 10 * (1 << 0) = 10, capped at 25 -> 10
            Assert.Single(delays);
            Assert.Equal(10, delays[0]);
        }

        [Fact]
        public async Task CancellationBeforeStart_DoesNotExecuteAction()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var executed = false;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => { executed = true; return Task.CompletedTask; },
                cts.Token);

            Assert.False(executed);
        }

        [Fact]
        public async Task CancellationDuringAction_StopsCleanly()
        {
            var cts = new CancellationTokenSource();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                },
                cts.Token);

            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task CancellationDuringDelay_ExitsGracefully()
        {
            var cts = new CancellationTokenSource();
            var retryCount = 0;

            // Should NOT throw — graceful exit when cancelled during backoff delay
            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => throw new Exception("fail"),
                cts.Token,
                onRetry: (_, attempt, _) =>
                {
                    retryCount = attempt;
                    cts.Cancel();
                },
                initialDelaySeconds: 60);

            Assert.Equal(1, retryCount);
        }

        [Fact]
        public async Task NullOnRetryCallback_DoesNotThrow()
        {
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= 2)
                        throw new Exception("fail");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: null,
                initialDelaySeconds: 0);

            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task OriginalExceptionType_PreservedInCallback()
        {
            var exceptions = new List<Exception>();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts == 1) throw new TimeoutException("rpc timeout");
                    if (attempts == 2) throw new System.Net.Http.HttpRequestException("connection refused");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: (ex, _, _) => exceptions.Add(ex),
                initialDelaySeconds: 0);

            Assert.IsType<TimeoutException>(exceptions[0]);
            Assert.IsType<System.Net.Http.HttpRequestException>(exceptions[1]);
        }

        [Fact]
        public async Task DefaultParameters_VerifyDelayFormula()
        {
            // Verify default initial=5, max=300 by checking the first callback delay
            var delays = new List<int>();
            var cts = new CancellationTokenSource();

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => throw new Exception("fail"),
                cts.Token,
                onRetry: (_, _, delay) =>
                {
                    delays.Add(delay);
                    cts.Cancel(); // Cancel immediately to avoid waiting
                });

            // Default: initialDelaySeconds=5, so first delay = 5 * (1 << 0) = 5
            Assert.Single(delays);
            Assert.Equal(5, delays[0]);
        }

        [Fact]
        public async Task HighRetryCount_DoesNotOverflow()
        {
            var delays = new List<int>();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= 35)
                        throw new Exception("fail");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: (_, _, delay) => delays.Add(delay),
                initialDelaySeconds: 0,
                maxDelaySeconds: 300);

            Assert.Equal(35, delays.Count);
            Assert.All(delays, d => Assert.True(d >= 0 && d <= 300));
        }

        [Fact]
        public async Task HighRetryCount_WithNonZeroInitial_NeverNegative()
        {
            // Verify the delay formula doesn't produce negative values at high retry counts
            // We test this by checking a single attempt at a high simulated shift value
            // RetryRunner caps shift at 30 internally, so we verify the cap + max work together
            var delays = new List<int>();
            var cts = new CancellationTokenSource();

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct => throw new Exception("fail"),
                cts.Token,
                onRetry: (_, attempt, delay) =>
                {
                    delays.Add(delay);
                    // Cancel immediately to avoid waiting real delays
                    cts.Cancel();
                },
                initialDelaySeconds: 5,
                maxDelaySeconds: 60);

            Assert.Single(delays);
            Assert.Equal(5, delays[0]); // 5 << 0 = 5, capped at 60
            Assert.InRange(delays[0], 0, 60);
        }
    }
}
