using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Validation;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    internal sealed class OrchestratorRetryRun : IDisposable
    {
        public const ulong TipBlock = 1_000;

        public CharLog Log { get; } = new();
        public CancellationTokenSource Cts { get; } = new();
        public Task<SnapBootstrapper.Result> Task { get; }
        public int FetchCalls;

        public static Exception TaskSetStalled() => new SnapSyncClient.SnapTaskSetStalledException(0, TimeSpan.FromSeconds(1), 0, null);

        public static Exception LeaseStalled() => new SnapSyncClient.SnapTaskLeaseStalledException(TimeSpan.FromSeconds(1), null, null);

        public static Exception Generic() => new InvalidOperationException("peer data rejected");

        public OrchestratorRetryRun(params Func<Exception>[] failures)
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var tip = new CanonicalTip
            {
                BlockNumber = TipBlock,
                BlockHash = CharBytes.Hash32(0x10),
                StateRoot = CharBytes.Hash32(0x20),
            };
            var scheduler = new CharScheduler
            {
                AccountRange = (root, start, limit, ct) =>
                {
                    var n = Interlocked.Increment(ref FetchCalls);
                    throw failures[Math.Min(n, failures.Length) - 1]();
                },
            };
            Task = SnapSyncOrchestrator.RunAsync(
                bundle, new CharPool(), scheduler, new CharTipSource(tip), activations: null, Log,
                new SnapSyncOrchestratorOptions
                {
                    UseBackwardSkeleton = false,
                    RunHistoryBackfill = false,
                    AccountConcurrency = 1,
                },
                Cts.Token);
        }

        public async Task StopAsync()
        {
            Cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task);
        }

        public void Dispose() => Cts.Dispose();
    }

    public class SnapSyncOrchestratorRetryCharacterizationTests
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

        [Fact]
        public async Task Given_ATaskSetStall_When_TheFirstAttemptFails_Then_ItIsRecycledAtWarningLevelWithTheInitialBackoff()
        {
            using var run = new OrchestratorRetryRun(OrchestratorRetryRun.TaskSetStalled);
            Assert.True(await run.Log.WaitForAsync("snap.phase2.recycle", Wait));
            await run.StopAsync();

            var entry = Assert.Single(run.Log.With("snap.phase2.recycle"));
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal("snap.phase2.recycle reason=taskset_stalled attempt=1; retrying in 2000ms", entry.Message);
            Assert.IsType<SnapSyncClient.SnapTaskSetStalledException>(entry.Exception);
        }

        [Fact]
        public async Task Given_ALeaseStall_When_TheFirstAttemptFails_Then_ItIsRecycledAtWarningLevelWithTheInitialBackoff()
        {
            using var run = new OrchestratorRetryRun(OrchestratorRetryRun.LeaseStalled);
            Assert.True(await run.Log.WaitForAsync("snap.phase2.recycle", Wait));
            await run.StopAsync();

            var entry = Assert.Single(run.Log.With("snap.phase2.recycle"));
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal("snap.phase2.recycle reason=lease_stalled attempt=1; retrying in 2000ms", entry.Message);
            Assert.IsType<SnapSyncClient.SnapTaskLeaseStalledException>(entry.Exception);
        }

        [Fact]
        public async Task Given_AnyOtherFailure_When_TheFirstAttemptFails_Then_ItIsRetriedAtWarningLevelNamingTheExceptionType()
        {
            using var run = new OrchestratorRetryRun(OrchestratorRetryRun.Generic);
            Assert.True(await run.Log.WaitForAsync("snap.bootstrap.retry", Wait));
            await run.StopAsync();

            var entry = Assert.Single(run.Log.With("snap.bootstrap.retry"));
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Equal(
                "snap.bootstrap.retry attempt=1 reason=InvalidOperationException; retrying in 2000ms (no genesis fallback)",
                entry.Message);
            Assert.False(run.Log.Saw("snap.bootstrap.retry.stalled"));
        }

        [Fact]
        public async Task Given_ConsecutiveGenericFailures_When_TheThirdAttemptFails_Then_TheBackoffHasDoubledAndTheRetryIsEscalatedToAnError()
        {
            using var run = new OrchestratorRetryRun(OrchestratorRetryRun.Generic);
            Assert.True(await run.Log.WaitForAsync("snap.bootstrap.retry.stalled", Wait));
            await run.StopAsync();

            var retries = run.Log.With("snap.bootstrap.retry");
            Assert.Equal(3, retries.Count);
            Assert.Equal(LogLevel.Warning, retries[0].Level);
            Assert.Contains("attempt=1 reason=InvalidOperationException; retrying in 2000ms", retries[0].Message);
            Assert.Equal(LogLevel.Warning, retries[1].Level);
            Assert.Contains("attempt=2 reason=InvalidOperationException; retrying in 4000ms", retries[1].Message);
            Assert.Equal(LogLevel.Error, retries[2].Level);
            Assert.StartsWith(
                "snap.bootstrap.retry.stalled attempt=3 reason=InvalidOperationException; 3+ consecutive failures", retries[2].Message);
            Assert.Contains("retrying in 8000ms", retries[2].Message);
            Assert.Contains("investigate (no genesis fallback)", retries[2].Message);
        }
    }

    public class SnapSyncOrchestratorTaskSetEscalationCharacterizationTests
    {
        [Fact]
        public async Task Given_ConsecutiveTaskSetStalls_When_TheThirdAttemptFails_Then_TheBackoffHasDoubledAndTheRecycleIsEscalatedToAnError()
        {
            using var run = new OrchestratorRetryRun(OrchestratorRetryRun.TaskSetStalled);
            Assert.True(await run.Log.WaitForAsync("3+ consecutive failures", TimeSpan.FromSeconds(30)));
            await run.StopAsync();

            var recycles = run.Log.With("snap.phase2.recycle");
            Assert.Equal(3, recycles.Count);
            Assert.Equal(LogLevel.Warning, recycles[0].Level);
            Assert.Equal("snap.phase2.recycle reason=taskset_stalled attempt=1; retrying in 2000ms", recycles[0].Message);
            Assert.Equal(LogLevel.Warning, recycles[1].Level);
            Assert.Equal("snap.phase2.recycle reason=taskset_stalled attempt=2; retrying in 4000ms", recycles[1].Message);
            Assert.Equal(LogLevel.Error, recycles[2].Level);
            Assert.Equal(
                "snap.phase2.recycle reason=taskset_stalled attempt=3; 3+ consecutive failures - retrying in 8000ms.",
                recycles[2].Message);
        }
    }

    public class SnapSyncOrchestratorLeaseEscalationCharacterizationTests
    {
        [Fact]
        public async Task Given_ConsecutiveLeaseStalls_When_TheThirdAttemptFails_Then_TheBackoffHasDoubledAndTheRecycleIsEscalatedToAnError()
        {
            using var run = new OrchestratorRetryRun(OrchestratorRetryRun.LeaseStalled);
            Assert.True(await run.Log.WaitForAsync("3+ consecutive failures", TimeSpan.FromSeconds(30)));
            await run.StopAsync();

            var recycles = run.Log.With("snap.phase2.recycle");
            Assert.Equal(3, recycles.Count);
            Assert.Equal(LogLevel.Warning, recycles[0].Level);
            Assert.Equal("snap.phase2.recycle reason=lease_stalled attempt=1; retrying in 2000ms", recycles[0].Message);
            Assert.Equal(LogLevel.Warning, recycles[1].Level);
            Assert.Equal("snap.phase2.recycle reason=lease_stalled attempt=2; retrying in 4000ms", recycles[1].Message);
            Assert.Equal(LogLevel.Error, recycles[2].Level);
            Assert.Equal(
                "snap.phase2.recycle reason=lease_stalled attempt=3; 3+ consecutive failures - retrying in 8000ms.",
                recycles[2].Message);
        }
    }

    public class SnapSyncOrchestratorMixedFailureSequenceCharacterizationTests
    {
        [Fact]
        public async Task Given_FailuresOfDifferentKinds_When_EachAttemptFails_Then_TheAttemptCounterAndBackoffAreSharedAcrossTheHandlers()
        {
            using var run = new OrchestratorRetryRun(
                OrchestratorRetryRun.TaskSetStalled, OrchestratorRetryRun.LeaseStalled, OrchestratorRetryRun.Generic);
            Assert.True(await run.Log.WaitForAsync("snap.bootstrap.retry.stalled", TimeSpan.FromSeconds(30)));
            await run.StopAsync();

            Assert.Equal(
                "snap.phase2.recycle reason=taskset_stalled attempt=1; retrying in 2000ms",
                Assert.Single(run.Log.With("reason=taskset_stalled")).Message);
            Assert.Equal(
                "snap.phase2.recycle reason=lease_stalled attempt=2; retrying in 4000ms",
                Assert.Single(run.Log.With("reason=lease_stalled")).Message);
            var escalated = Assert.Single(run.Log.With("snap.bootstrap.retry.stalled"));
            Assert.Equal(LogLevel.Error, escalated.Level);
            Assert.Contains("attempt=3", escalated.Message);
            Assert.Contains("retrying in 8000ms", escalated.Message);
        }
    }
}
