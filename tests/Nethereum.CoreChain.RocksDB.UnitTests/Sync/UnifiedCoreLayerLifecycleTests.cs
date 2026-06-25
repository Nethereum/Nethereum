using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class UnifiedCoreLayerLifecycleTests : IDisposable
    {
        private readonly string _dataDir;

        public UnifiedCoreLayerLifecycleTests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"unified_core_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private static byte[] StateRootFor(ulong blockNumber)
        {
            var bytes = new byte[32];
            bytes[31] = (byte)(blockNumber & 0xFF);
            bytes[30] = (byte)((blockNumber >> 8) & 0xFF);
            return bytes;
        }

        private static byte[] HashFor(ulong blockNumber)
        {
            var bytes = new byte[32];
            bytes[0] = 0xBB;
            bytes[31] = (byte)(blockNumber & 0xFF);
            bytes[30] = (byte)((blockNumber >> 8) & 0xFF);
            return bytes;
        }

        private static List<BlockBundle> MakeBundles(ulong from, ulong to)
        {
            var list = new List<BlockBundle>();
            for (ulong i = from; i <= to; i++)
            {
                list.Add(new BlockBundle(
                    Header: new BlockHeader { BlockNumber = i, StateRoot = StateRootFor(i) },
                    Transactions: new List<ISignedTransaction>(),
                    Uncles: new List<BlockHeader>(),
                    Withdrawals: null,
                    HeaderHash: HashFor(i)));
            }
            return list;
        }

        private sealed class ScriptedExecutor : IBlockExecutor
        {
            private readonly HashSet<ulong> _mismatchBlocks;
            public int CallCount { get; private set; }
            public ScriptedExecutor(params ulong[] mismatchBlocks)
            {
                _mismatchBlocks = new HashSet<ulong>(mismatchBlocks);
            }

            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header,
                IList<ISignedTransaction> transactions,
                IList<BlockHeader> uncles,
                IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                CallCount++;
                var bn = (ulong)header.BlockNumber;
                var isMismatch = _mismatchBlocks.Contains(bn);
                var computed = isMismatch
                    ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }
                    : header.StateRoot;
                return Task.FromResult(new BlockImporterResult
                {
                    ComputedStateRoot = computed,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = isMismatch,
                });
            }
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public ValidationAction Verdict { get; set; } = ValidationAction.RewindAndRetry;
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => Verdict;
        }

        [Fact]
        public async Task UnifiedCoreLayer_MultipleSources_ChainedRunAsyncSyncsFullRange()
        {
            var source1 = new LocalReplayBlockSource(MakeBundles(1, 15));
            var source2 = new LocalReplayBlockSource(MakeBundles(16, 30));
            var follower = new FollowerService();
            var policy = new FixedPolicy();

            IChainStoreBundle firstBundle = null;
            var firstRun = await follower.RunAsync(
                source1,
                bundleFactory: () => firstBundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive),
                executorFactory: _ => new ScriptedExecutor(),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 10, AnchorEvery: 0),
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, firstRun.ExitReason);
            Assert.Equal(15UL, firstRun.LastExecutedBlock);
            Assert.Equal(15UL, firstRun.BlocksExecuted);
            await firstBundle.DisposeAsync();

            IChainStoreBundle secondBundle = null;
            var secondRun = await follower.RunAsync(
                source2,
                bundleFactory: () => secondBundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive),
                executorFactory: _ => new ScriptedExecutor(),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 16, CheckpointEvery: 10, AnchorEvery: 0),
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, secondRun.ExitReason);
            Assert.Equal(30UL, secondRun.LastExecutedBlock);
            Assert.Equal(15UL, secondRun.BlocksExecuted);
            await secondBundle.DisposeAsync();

            using var finalBundle = RocksDbChainStoreBundle.Open(_dataDir);
            Assert.Equal(30UL, finalBundle.Metadata.GetLastBlock());
            Assert.NotNull(finalBundle.Metadata.GetCheckpoint(10));
            Assert.NotNull(finalBundle.Metadata.GetCheckpoint(20));
            Assert.NotNull(finalBundle.Metadata.GetCheckpoint(30));
            Assert.True(Directory.Exists(finalBundle.ResolveCheckpointSnapshotPath(10)));
            Assert.True(Directory.Exists(finalBundle.ResolveCheckpointSnapshotPath(30)));
        }

        [Fact]
        public async Task UnifiedCoreLayer_DivergenceMidStream_TriggersSnapshotRestore_AndResumes()
        {
            var bundles = MakeBundles(1, 30);
            var executorWithBug = new ScriptedExecutor(25UL);
            var executorFixed = new ScriptedExecutor();
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };

            IChainStoreBundle divergedBundle = null;
            var diverged = await follower.RunAsync(
                new LocalReplayBlockSource(bundles),
                bundleFactory: () => divergedBundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive),
                executorFactory: _ => executorWithBug,
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 10, AnchorEvery: 0, MaxRewindCycles: 1),
                ct: default);

            Assert.Equal(FollowerExitReason.SnapshotRestoreRequested, diverged.ExitReason);
            Assert.NotNull(diverged.SnapshotRestoreTarget);
            Assert.Equal(20UL, diverged.SnapshotRestoreTarget!.Value.BlockNumber);
            Assert.Equal(24UL, diverged.LastExecutedBlock);
            Assert.Equal(1UL, diverged.RootMismatches);
            await divergedBundle.DisposeAsync();

            var snapshotDir = Path.Combine(_dataDir, ".cp", "000000000020");
            Assert.True(Directory.Exists(snapshotDir));
            RocksDbChainStoreBundle.RestoreFromCheckpointDir(snapshotDir, _dataDir);

            using (var afterRestore = RocksDbChainStoreBundle.Open(_dataDir))
            {
                Assert.Equal(20UL, afterRestore.Metadata.GetLastBlock());
            }

            IChainStoreBundle resumedBundle = null;
            var resumed = await follower.RunAsync(
                new LocalReplayBlockSource(bundles),
                bundleFactory: () => resumedBundle = RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive),
                executorFactory: _ => executorFixed,
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 21, CheckpointEvery: 10, AnchorEvery: 0),
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, resumed.ExitReason);
            Assert.Equal(30UL, resumed.LastExecutedBlock);
            Assert.Equal(10UL, resumed.BlocksExecuted);
            await resumedBundle.DisposeAsync();

            using var finalBundle = RocksDbChainStoreBundle.Open(_dataDir);
            Assert.Equal(30UL, finalBundle.Metadata.GetLastBlock());
            Assert.NotNull(finalBundle.Metadata.GetCheckpoint(10));
            Assert.NotNull(finalBundle.Metadata.GetCheckpoint(30));
            Assert.True(Directory.Exists(finalBundle.ResolveCheckpointSnapshotPath(20)));
        }

        [Fact]
        public async Task UnifiedCoreLayer_DivergenceWithNoSnapshot_ReturnsRewindUnavailable()
        {
            var bundles = MakeBundles(1, 5);
            var executorWithBug = new ScriptedExecutor(3UL);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(bundles),
                bundleFactory: () => RocksDbChainStoreBundle.Open(_dataDir, HistoricalStateOptions.FullArchive),
                executorFactory: _ => executorWithBug,
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 100, AnchorEvery: 0, MaxRewindCycles: 1),
                ct: default);

            Assert.Equal(FollowerExitReason.RewindUnavailable, result.ExitReason);
            Assert.Equal(2UL, result.LastExecutedBlock);
            Assert.Equal(1UL, result.RootMismatches);
        }
    }
}
