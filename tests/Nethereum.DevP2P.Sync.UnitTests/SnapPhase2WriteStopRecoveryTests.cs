using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model.P2P.Snap;
using Microsoft.Extensions.Logging;
using Nethereum.DevP2P.Sync.Metrics;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Abstractions;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapPhase2WriteStopRecoveryTests
    {
        private sealed class UnusedSnapPeer : ISnapPeer
        {
            public Task<AccountRangeMessage> GetAccountRangeAsync(GetAccountRangeMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<StorageRangesMessage> GetStorageRangesAsync(GetStorageRangesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<ByteCodesMessage> GetByteCodesAsync(GetByteCodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
            public Task<TrieNodesMessage> GetTrieNodesAsync(GetTrieNodesMessage r, CancellationToken ct = default)
                => throw new NotSupportedException();
        }

        private static SnapSyncClient NewClient()
            => new SnapSyncClient(new UnusedSnapPeer()) { StateBackpressurePollMs = 10 };

        [Fact]
        public async Task Consumer_PausesWhileWriteStopped_ThenResumesAndProgresses_WhenStopClears()
        {
            var client = NewClient();

            var writeStopped = true;
            client.StateWriteBackpressure = () => Volatile.Read(ref writeStopped)
                ? "state WRITE-STOP: rocksdb.is-write-stopped=1 (test)"
                : null;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            int progress = 0;
            var consumer = Task.Run(async () =>
            {
                await client.WaitWhileStateBackpressuredAsync(consumerIdx: 0, cts.Token);
                Interlocked.Increment(ref progress);
            });

            await Task.Delay(200, cts.Token);
            Assert.False(consumer.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref progress));

            Volatile.Write(ref writeStopped, false);
            await consumer;
            Assert.Equal(1, Volatile.Read(ref progress));
        }

        [Fact]
        public async Task Consumer_PersistentWriteStop_StaysParked_AndIsCancellable()
        {
            var client = NewClient();
            client.StateWriteBackpressure = () => "persistent write-stop (test)";

            using var cts = new CancellationTokenSource();
            var consumer = client.WaitWhileStateBackpressuredAsync(consumerIdx: 0, cts.Token);

            await Task.Delay(200);
            Assert.False(consumer.IsCompleted);

            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await consumer);
        }

        [Fact]
        public async Task Given_StorageBackpressureLastingManyPolls_When_AConsumerWaits_Then_ItStaysParkedWithoutFailingThePhaseAndResumesWhenPressureClears()
        {
            var client = NewClient();

            var polls = 0;
            client.StateWriteBackpressure = () => Interlocked.Increment(ref polls) <= 60
                ? "state flush-pipeline saturation (test)"
                : null;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.WaitWhileStateBackpressuredAsync(consumerIdx: 3, cts.Token);

            Assert.True(Volatile.Read(ref polls) > 60);
        }

        [Fact]
        public async Task Given_StorageBackpressureHeldPastTheWarnThreshold_When_ConsumerZeroWaits_Then_ThePauseLineEscalatesFromInformationToWarningAndTheMetricTracksThePauseUntilItClears()
        {
            using var metrics = new SnapSyncMetrics("test");
            var logger = new CapturingLogger();
            var client = new SnapSyncClient(new UnusedSnapPeer(), sink: null, logger: logger, metrics: metrics)
            {
                StateBackpressurePollMs = 10,
                StateBackpressureWarnAfter = TimeSpan.FromMilliseconds(300),
            };

            var released = false;
            client.StateWriteBackpressure = () => Volatile.Read(ref released) ? null : "state WRITE-STOP (test)";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var consumer = client.WaitWhileStateBackpressuredAsync(consumerIdx: 0, cts.Token);

            while (!logger.Snapshot().Any(e => e.Level == LogLevel.Warning))
                await Task.Delay(10, cts.Token);
            var pausedWhileHeld = metrics.Phase2BackpressurePausedFor;

            Volatile.Write(ref released, true);
            await consumer;

            var entries = logger.Snapshot();
            var paused = entries.Where(e => e.Message.StartsWith("Phase 2 leaf stream: paused for")).ToList();
            Assert.Equal(LogLevel.Information, paused[0].Level);
            var firstWarning = paused.FindIndex(e => e.Level == LogLevel.Warning);
            Assert.True(firstWarning > 0);
            Assert.Contains("storage backpressure has not released for over", paused[firstWarning].Message);
            Assert.Contains("state WRITE-STOP (test)", paused[firstWarning].Message);
            Assert.All(paused.Skip(firstWarning), e => Assert.Equal(LogLevel.Warning, e.Level));
            Assert.True(pausedWhileHeld >= TimeSpan.FromMilliseconds(300), $"paused metric was {pausedWhileHeld}");
            Assert.Equal(TimeSpan.Zero, metrics.Phase2BackpressurePausedFor);
            var resumed = Assert.Single(entries, e => e.Message.StartsWith("Phase 2 leaf stream: resumed after"));
            Assert.Equal(LogLevel.Information, resumed.Level);
            Assert.DoesNotContain(entries, e => e.Level >= LogLevel.Error);
        }

        [Fact]
        public async Task Given_AParkedConsumerCancelledByAPivotMoveBeforeTheValveReleases_When_AFreshConsumerObservesTheRelease_Then_ThePausedForGaugeReadsZeroAndResumedIsLoggedOnce()
        {
            using var metrics = new SnapSyncMetrics("test");
            var logger = new CapturingLogger();
            var client = new SnapSyncClient(new UnusedSnapPeer(), sink: null, logger: logger, metrics: metrics)
            {
                StateBackpressurePollMs = 10,
            };

            var released = false;
            client.StateWriteBackpressure = () => Volatile.Read(ref released) ? null : "state WRITE-STOP (test)";

            using var attemptCts = new CancellationTokenSource();
            var parked = client.WaitWhileStateBackpressuredAsync(consumerIdx: 0, attemptCts.Token);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (metrics.Phase2BackpressurePausedFor < TimeSpan.FromMilliseconds(50))
                await Task.Delay(10, timeout.Token);

            attemptCts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await parked);
            Assert.NotEqual(TimeSpan.Zero, metrics.Phase2BackpressurePausedFor);

            Volatile.Write(ref released, true);
            await client.WaitWhileStateBackpressuredAsync(consumerIdx: 0, timeout.Token);

            Assert.Equal(TimeSpan.Zero, metrics.Phase2BackpressurePausedFor);
            var resumed = Assert.Single(logger.Snapshot(), e => e.Message.StartsWith("Phase 2 leaf stream: resumed after"));
            Assert.Equal(LogLevel.Information, resumed.Level);
        }

        [Fact]
        public async Task Drain_UndrainableConsumers_AbandonsWithinBound_DoesNotHang()
        {
            var client = NewClient();
            client.Phase2DrainTimeout = TimeSpan.FromMilliseconds(200);

            var neverDrains = new TaskCompletionSource<bool>();

            var sw = Stopwatch.StartNew();
            var drain = client.DrainPhase2AttemptAsync(
                neverDrains.Task, new InvalidOperationException("supervisor tripped"), CancellationToken.None);
            var finished = await Task.WhenAny(drain, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(drain, finished);

            var ex = await Assert.ThrowsAsync<SnapSyncClient.SnapPhase2UndrainedException>(async () => await drain);
            Assert.Equal(TimeSpan.FromMilliseconds(200), ex.DrainTimeout);
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), "drain must return promptly, not hang");

            neverDrains.SetException(new InvalidOperationException("late native fault after abandonment"));
            await Task.Delay(50);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly List<(LogLevel Level, string Message)> _entries = new();

            public List<(LogLevel Level, string Message)> Snapshot()
            {
                lock (_entries) return new List<(LogLevel Level, string Message)>(_entries);
            }

            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (formatter == null) return;
                lock (_entries) _entries.Add((logLevel, formatter(state, exception)));
            }
        }

        [Fact]
        public async Task Drain_ConsumersDrainWithinBound_ReturnsCleanly()
        {
            var client = NewClient();
            client.Phase2DrainTimeout = TimeSpan.FromSeconds(5);

            var drained = Task.CompletedTask;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.DrainPhase2AttemptAsync(
                drained, new InvalidOperationException("supervisor tripped"), cts.Token);
        }
    }
}
