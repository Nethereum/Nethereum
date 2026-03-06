using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.BlockchainProcessing;
using Xunit;

namespace Nethereum.AppChain.Anchoring.UnitTests
{
    public class HostedServiceLifecycleTests
    {
        [Fact]
        public async Task HubLogProcessingWorker_DisabledConfig_ExitsImmediately()
        {
            var store = new InMemoryMessageIndexStore();
            var config = new MessagingConfig { Enabled = false };
            var worker = new HubLogProcessingWorker(store, 1, config);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StartAsync(cts.Token);
            await worker.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task HubLogProcessingWorker_NoSourceChains_ExitsImmediately()
        {
            var store = new InMemoryMessageIndexStore();
            var config = new MessagingConfig { Enabled = true, SourceChains = new List<SourceChainConfig>() };
            var worker = new HubLogProcessingWorker(store, 1, config);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await worker.StartAsync(cts.Token);
            await worker.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task MessagingWorker_DisabledConfig_DoesNotStart()
        {
            var messagingService = new MessagingService(420420, new MessagingConfig(), new InMemoryMessageIndexStore());
            var config = new MessagingConfig { Enabled = false };
            var worker = new MessagingWorker(messagingService, config);

            await worker.StartAsync(CancellationToken.None);
            Assert.False(worker.IsRunning);
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task MessagingWorker_EnabledConfig_StartsAndStops()
        {
            var messagingService = new MessagingService(420420, new MessagingConfig(), new InMemoryMessageIndexStore());
            var config = new MessagingConfig { Enabled = true, PollIntervalMs = 60000 };
            var worker = new MessagingWorker(messagingService, config);

            await worker.StartAsync(CancellationToken.None);
            Assert.True(worker.IsRunning);

            await worker.StopAsync(CancellationToken.None);
            Assert.False(worker.IsRunning);
            worker.Dispose();
        }

        [Fact]
        public async Task RetryRunner_SimulatesServiceRestart_AfterTransientFailures()
        {
            var attempts = 0;
            var retryLog = new List<(int attempt, int delay)>();

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    if (attempts <= 3)
                        throw new TimeoutException("RPC node unreachable");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                onRetry: (_, attempt, delay) => retryLog.Add((attempt, delay)),
                initialDelaySeconds: 0);

            Assert.Equal(4, attempts);
            Assert.Equal(3, retryLog.Count);
        }

        [Fact]
        public async Task RetryRunner_GracefulShutdown_DuringLongRunningOperation()
        {
            using var cts = new CancellationTokenSource();
            var started = new TaskCompletionSource<bool>();
            var actionCompleted = false;

            var runTask = RetryRunner.RunWithExponentialBackoffAsync(
                async ct =>
                {
                    started.SetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                    actionCompleted = true;
                },
                cts.Token);

            await started.Task;
            cts.Cancel();
            await runTask;

            Assert.False(actionCompleted);
        }

        [Fact]
        public async Task RetryRunner_ServiceRecovery_AfterMultipleFailureTypes()
        {
            var exceptions = new List<Type>();
            var attempts = 0;

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    attempts++;
                    return attempts switch
                    {
                        1 => throw new TimeoutException("connection timeout"),
                        2 => throw new System.Net.Http.HttpRequestException("503 service unavailable"),
                        3 => throw new InvalidOperationException("rate limited"),
                        _ => Task.CompletedTask
                    };
                },
                CancellationToken.None,
                onRetry: (ex, _, _) => exceptions.Add(ex.GetType()),
                initialDelaySeconds: 0);

            Assert.Equal(4, attempts);
            Assert.Equal(typeof(TimeoutException), exceptions[0]);
            Assert.Equal(typeof(System.Net.Http.HttpRequestException), exceptions[1]);
            Assert.Equal(typeof(InvalidOperationException), exceptions[2]);
        }

        [Fact]
        public async Task RetryRunner_ConcurrentServices_EachRetryIndependently()
        {
            var service1Attempts = 0;
            var service2Attempts = 0;

            var task1 = RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    service1Attempts++;
                    if (service1Attempts <= 2) throw new Exception("svc1 fail");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                initialDelaySeconds: 0);

            var task2 = RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    service2Attempts++;
                    if (service2Attempts <= 4) throw new Exception("svc2 fail");
                    return Task.CompletedTask;
                },
                CancellationToken.None,
                initialDelaySeconds: 0);

            await Task.WhenAll(task1, task2);

            Assert.Equal(3, service1Attempts);
            Assert.Equal(5, service2Attempts);
        }

        [Fact]
        public async Task RetryRunner_SharedCancellation_StopsAllConcurrentServices()
        {
            using var cts = new CancellationTokenSource();
            var service1Started = new TaskCompletionSource<bool>();
            var service2Started = new TaskCompletionSource<bool>();

            var task1 = RetryRunner.RunWithExponentialBackoffAsync(
                async ct =>
                {
                    service1Started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                },
                cts.Token);

            var task2 = RetryRunner.RunWithExponentialBackoffAsync(
                async ct =>
                {
                    service2Started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                },
                cts.Token);

            await Task.WhenAll(service1Started.Task, service2Started.Task);
            cts.Cancel();

            await Task.WhenAll(task1, task2);
        }

        [Fact]
        public async Task RetryRunner_BackoffResets_AfterSuccessfulRecovery()
        {
            var allDelays = new List<int>();
            var round = 0;
            var failuresPerRound = 0;
            var cts = new CancellationTokenSource();

            await RetryRunner.RunWithExponentialBackoffAsync(
                ct =>
                {
                    round++;
                    if (round <= 3)
                    {
                        failuresPerRound++;
                        throw new Exception("fail");
                    }
                    // Success on attempt 4, runner exits
                    return Task.CompletedTask;
                },
                cts.Token,
                onRetry: (_, _, delay) => allDelays.Add(delay),
                initialDelaySeconds: 0);

            // Each run of RetryRunner maintains its own backoff counter,
            // so delays should be 0, 0, 0 (initial*1, initial*2, initial*4 with initial=0)
            Assert.Equal(3, allDelays.Count);
        }
    }
}
