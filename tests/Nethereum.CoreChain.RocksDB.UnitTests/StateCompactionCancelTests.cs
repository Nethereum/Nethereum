using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using RocksDbSharp;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    [Collection(StateCompactionCollection.Name)]
    public class StateCompactionCancelTests : IDisposable
    {
        private const string TargetCf = RocksDbManager.CF_STATE_ACCOUNTS;
        private const string SecondCf = RocksDbManager.CF_STATE_STORAGE;
        private const int L0Files = 3;
        private const int EntriesPerFile = 20_000;
        private const int EntriesPerBatch = 20_000;
        private const int ValueBytes = 64;
        private const string FilesAtLevel0 = "rocksdb.num-files-at-level0";

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"statecompactcancel_{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (Directory.Exists(_root)) { try { Directory.Delete(_root, true); } catch { } }
        }

        private static void PopulateOverlappingLevelZeroFiles(RocksDbManager manager, string columnFamily = TargetCf)
        {
            var random = new Random(20261013);
            var key = new byte[32];
            var value = new byte[ValueBytes];
            for (var file = 0; file < L0Files; file++)
            {
                for (var written = 0; written < EntriesPerFile; written += EntriesPerBatch)
                {
                    using var batch = manager.CreateWriteBatch();
                    var cf = manager.GetColumnFamily(columnFamily);
                    for (var i = 0; i < EntriesPerBatch; i++)
                    {
                        random.NextBytes(key);
                        random.NextBytes(value);
                        batch.Put(key, value, cf);
                    }
                    manager.Write(batch);
                }
                FlushColumnFamily(manager, columnFamily);
            }
        }

        private static void FlushColumnFamily(RocksDbManager manager, string columnFamily)
        {
            using var lease = manager.Lease();
            Native.Instance.rocksdb_flush_cf(lease.Database.Handle, new FlushOptions().SetWaitForFlush(true).Handle, manager.GetColumnFamily(columnFamily).Handle);
        }

        private static void DisableAutomaticCompaction(RocksDbManager manager, string columnFamily)
        {
            using var lease = manager.Lease();
            Native.Instance.rocksdb_set_options_cf(lease.Database.Handle, manager.GetColumnFamily(columnFamily).Handle, 1,
                new[] { "disable_auto_compactions" }, new[] { "true" });
        }

        private static long LevelZeroFiles(RocksDbManager manager, string columnFamily = TargetCf)
        {
            using var lease = manager.Lease();
            return long.Parse(lease.Database.GetProperty(FilesAtLevel0, manager.GetColumnFamily(columnFamily)));
        }

        private (RocksDbManager Manager, RocksDbChainStoreBundle Bundle) OpenFixture(string name, bool withSecondColumnFamily = false)
        {
            var dir = Path.Combine(_root, name);
            Directory.CreateDirectory(dir);
            var manager = new RocksDbManager(new RocksDbStorageOptions { DatabasePath = dir, PathKeyedState = true });
            var bundle = RocksDbChainStoreBundle.FromManager(manager, dir);
            DisableAutomaticCompaction(manager, TargetCf);
            PopulateOverlappingLevelZeroFiles(manager);
            Assert.True(LevelZeroFiles(manager) >= L0Files);
            if (withSecondColumnFamily)
            {
                DisableAutomaticCompaction(manager, SecondCf);
                PopulateOverlappingLevelZeroFiles(manager, SecondCf);
                Assert.True(LevelZeroFiles(manager, SecondCf) >= L0Files);
            }
            return (manager, bundle);
        }

        private static readonly TimeSpan HangGuard = TimeSpan.FromMinutes(5);

        private static async Task<(List<string> Messages, long LevelZeroAfter)> CancelOnceCompactingAsync(
            RocksDbManager manager, RocksDbChainStoreBundle bundle)
        {
            using var cts = new CancellationTokenSource();
            var messages = new List<string>();
            var compaction = ((IStateCompaction)bundle).CompactStateAsync(
                message =>
                {
                    lock (messages) messages.Add(message);
                    if (message == $"compacting {TargetCf}...") cts.Cancel();
                },
                cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => compaction).WaitAsync(HangGuard);
            lock (messages) return (messages.ToList(), LevelZeroFiles(manager));
        }

        [Fact]
        public async Task Given_AStateCompactionThatHasStartedOnAColumnFamily_When_ItsTokenIsCancelled_Then_ItThrowsCancelledAndNeitherThatColumnFamilyNorTheRestIsCompacted()
        {
            var fixture = OpenFixture("cancelled");
            using (fixture.Manager)
            {
                var (messages, levelZeroAfter) = await CancelOnceCompactingAsync(fixture.Manager, fixture.Bundle);

                Assert.Contains($"compacting {TargetCf}...", messages);
                Assert.DoesNotContain(messages, m => m.Contains("compacted in"));
                Assert.Single(messages);
                Assert.True(levelZeroAfter >= L0Files, $"the cancelled column family was compacted anyway: {levelZeroAfter} level-0 files left");
            }
        }

        [Fact]
        public async Task Given_ACancelledStateCompaction_When_CompactStateAsyncRunsAgainWithoutCancellation_Then_LevelZeroOfTheCancelledColumnFamilyBecomesZero()
        {
            var fixture = OpenFixture("recompact");
            using (fixture.Manager)
            {
                var (_, levelZeroAfterCancel) = await CancelOnceCompactingAsync(fixture.Manager, fixture.Bundle);
                Assert.True(levelZeroAfterCancel >= L0Files);

                await ((IStateCompaction)fixture.Bundle).CompactStateAsync(null, CancellationToken.None)
                    .WaitAsync(HangGuard);

                Assert.Equal(0, LevelZeroFiles(fixture.Manager));
            }
        }

        [Fact]
        public async Task Given_AStateCompactionThatFinishedOneColumnFamily_When_ItsTokenIsCancelledBeforeTheNext_Then_TheNextColumnFamilyIsNeverStarted()
        {
            var fixture = OpenFixture("betweenfamilies", withSecondColumnFamily: true);
            using (fixture.Manager)
            {
                using var cts = new CancellationTokenSource();
                var messages = new List<string>();
                var compaction = ((IStateCompaction)fixture.Bundle).CompactStateAsync(
                    message =>
                    {
                        lock (messages) messages.Add(message);
                        if (message.StartsWith($"{TargetCf} compacted in")) cts.Cancel();
                    },
                    cts.Token);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => compaction).WaitAsync(HangGuard);

                lock (messages)
                {
                    Assert.Contains(messages, m => m.StartsWith($"{TargetCf} compacted in"));
                    Assert.DoesNotContain($"compacting {SecondCf}...", messages);
                }
                Assert.Equal(0, LevelZeroFiles(fixture.Manager));
                Assert.True(LevelZeroFiles(fixture.Manager, SecondCf) >= L0Files);
            }
        }
        [Fact]
        public async Task Given_AStateCompaction_When_ItRuns_Then_TheBlockingCompactionWorkRunsOffTheSharedThreadPool()
        {
            var fixture = OpenFixture("dedicated-thread");
            using (fixture.Manager)
            {
                var onPoolThread = new List<bool>();

                await ((IStateCompaction)fixture.Bundle).CompactStateAsync(
                    message => { lock (onPoolThread) onPoolThread.Add(Thread.CurrentThread.IsThreadPoolThread); },
                    CancellationToken.None).WaitAsync(HangGuard);

                Assert.NotEmpty(onPoolThread);
                Assert.DoesNotContain(true, onPoolThread);
            }
        }

    }
}
