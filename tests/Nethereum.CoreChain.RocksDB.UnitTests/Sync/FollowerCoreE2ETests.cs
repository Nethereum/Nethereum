using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    public class FollowerCoreE2ETests : IDisposable
    {
        private readonly string _dataDir;
        private IChainStoreBundle _activeBundle;

        public FollowerCoreE2ETests()
        {
            _dataDir = Path.Combine(Path.GetTempPath(), $"follower_core_e2e_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            try { _activeBundle?.Dispose(); } catch { }
            if (Directory.Exists(_dataDir))
            {
                try { Directory.Delete(_dataDir, recursive: true); } catch { }
            }
        }

        private sealed class FixedPolicy : IValidationPolicy
        {
            public ValidationAction Verdict { get; set; } = ValidationAction.RewindAndRetry;
            public bool ShouldAnchorAt(ulong b) => false;
            public ValidationAction OnVerdict(DivergenceVerdict v, ulong b) => Verdict;
        }

        [Fact]
        public async Task Fixture_LoadChainAndGenesis_SanityCheck()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            Assert.True(HiveTestdataFixture.Chain.Count > 0,
                "chain.rlp should decode at least one block bundle");

            var firstBundle = HiveTestdataFixture.Chain[0];
            Assert.True(firstBundle.Header.BlockNumber > 0,
                "Hive chain.rlp first bundle is expected to be block 1+ (genesis lives in genesis.json)");

            Assert.True(HiveTestdataFixture.GenesisAllocRaw.Count > 0,
                "genesis.json alloc should contain at least one account");

            var stateStore = new InMemoryStateStore();
            await HiveTestdataFixture.PopulateGenesisAsync(stateStore);

            var firstAllocKey = HiveTestdataFixture.GenesisAllocRaw.Keys.First();
            var lookupAddr = firstAllocKey.StartsWith("0x") ? firstAllocKey : "0x" + firstAllocKey;
            var loaded = await stateStore.GetAccountAsync(lookupAddr);
            Assert.NotNull(loaded);
        }

        [Fact]
        public async Task ForwardSync_HiveChain_StateRootsMatchCanonical()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle => FollowerStackBuilder.Build(
                    bundle,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: System.Threading.CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(expectedBlocks, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir);
            Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task ForwardSync_WithCheckpointEvery50_CreatesMetadataAndSnapshotDirs()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;
            const ulong checkpointEvery = 50UL;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle => FollowerStackBuilder.Build(
                    bundle,
                    HiveTestdataFixture.ChainActivations,
                    HiveTestdataFixture.HardforkConfigFactory,
                    HiveTestdataFixture.ChainConfigFactory),
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: checkpointEvery, AnchorEvery: 0),
                ct: System.Threading.CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(expectedBlocks, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());

            var expectedCheckpointBlocks = new System.Collections.Generic.List<ulong>();
            for (ulong b = checkpointEvery; b <= lastChainBlock; b += checkpointEvery)
            {
                expectedCheckpointBlocks.Add(b);
            }

            Assert.True(expectedCheckpointBlocks.Count > 0,
                "Hive chain should be long enough to produce at least one checkpoint at CheckpointEvery=50");

            foreach (var cpBlock in expectedCheckpointBlocks)
            {
                var snapshotDir = reopened.ResolveCheckpointSnapshotPath(cpBlock);
                Assert.True(Directory.Exists(snapshotDir),
                    $"Expected .cp/ snapshot dir at {snapshotDir} for checkpoint block {cpBlock}");

                var cp = reopened.Metadata.GetCheckpoint(cpBlock);
                Assert.NotNull(cp);
                Assert.Equal(cpBlock, cp!.Value.BlockNumber);
                Assert.NotNull(cp.Value.StateRoot);
                Assert.NotEmpty(cp.Value.StateRoot);

                ulong nonCheckpointBlock = cpBlock + 1;
                if (nonCheckpointBlock <= lastChainBlock && nonCheckpointBlock % checkpointEvery != 0)
                {
                    Assert.Null(reopened.Metadata.GetCheckpoint(nonCheckpointBlock));
                }
            }

            var listed = await reopened.ListCheckpointsAsync();
            var listedBlocks = listed.Select(c => c.BlockNumber).OrderBy(b => b).ToList();
            Assert.Equal(expectedCheckpointBlocks, listedBlocks);

            var metadataBlocks = reopened.Metadata.ListCheckpointBlockNumbers().OrderBy(b => b).ToList();
            Assert.Equal(expectedCheckpointBlocks, metadataBlocks);

            var archiveDir = Path.Combine(_dataDir, ".cp");
            Assert.True(Directory.Exists(archiveDir), $".cp archive dir should exist at {archiveDir}");
            var snapshotDirsOnDisk = Directory.EnumerateDirectories(archiveDir)
                .Select(p => ulong.Parse(Path.GetFileName(p)))
                .OrderBy(b => b)
                .ToList();
            Assert.Equal(expectedCheckpointBlocks, snapshotDirsOnDisk);
        }

        private sealed class PredicateTamperExecutor : IBlockExecutor
        {
            private readonly IBlockExecutor _inner;
            private readonly Func<ulong, bool> _shouldTamper;
            private int _tampered;

            public PredicateTamperExecutor(IBlockExecutor inner, Func<ulong, bool> shouldTamper)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _shouldTamper = shouldTamper ?? throw new ArgumentNullException(nameof(shouldTamper));
            }

            public int TamperedCalls => _tampered;

            public async Task<BlockImporterResult> ProcessBlockAsync(
                Nethereum.Model.BlockHeader header,
                System.Collections.Generic.IList<Nethereum.Model.ISignedTransaction> transactions,
                System.Collections.Generic.IList<Nethereum.Model.BlockHeader> uncles,
                System.Collections.Generic.IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                    .ConfigureAwait(false);

                if (_shouldTamper((ulong)header.BlockNumber))
                {
                    Interlocked.Increment(ref _tampered);
                    result = new BlockImporterResult
                    {
                        Fork = result.Fork,
                        ComputedStateRoot = new byte[32],
                        ExpectedStateRoot = result.ExpectedStateRoot,
                        StateRootMismatch = true,
                        TransactionsExecuted = result.TransactionsExecuted,
                        MinerRewardCredited = result.MinerRewardCredited,
                        WithdrawalsCredited = result.WithdrawalsCredited,
                        BlockHash = result.BlockHash,
                        ErrorMessage = result.ErrorMessage,
                        Exception = result.Exception,
                        ExecutionResults = result.ExecutionResults,
                    };
                }

                return result;
            }
        }

        private sealed class LyingOnceExecutor : IBlockExecutor
        {
            private readonly IBlockExecutor _inner;
            private readonly ulong _targetBlock;
            private int _tampered;

            public LyingOnceExecutor(IBlockExecutor inner, ulong targetBlock)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _targetBlock = targetBlock;
            }

            public int TamperedCalls => _tampered;

            public async Task<BlockImporterResult> ProcessBlockAsync(
                Nethereum.Model.BlockHeader header,
                System.Collections.Generic.IList<Nethereum.Model.ISignedTransaction> transactions,
                System.Collections.Generic.IList<Nethereum.Model.BlockHeader> uncles,
                System.Collections.Generic.IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct)
                    .ConfigureAwait(false);

                if ((ulong)header.BlockNumber == _targetBlock
                    && Interlocked.CompareExchange(ref _tampered, 1, 0) == 0)
                {
                    result = new BlockImporterResult
                    {
                        Fork = result.Fork,
                        ComputedStateRoot = new byte[32],
                        ExpectedStateRoot = result.ExpectedStateRoot,
                        StateRootMismatch = true,
                        TransactionsExecuted = result.TransactionsExecuted,
                        MinerRewardCredited = result.MinerRewardCredited,
                        WithdrawalsCredited = result.WithdrawalsCredited,
                        BlockHash = result.BlockHash,
                        ErrorMessage = result.ErrorMessage,
                        Exception = result.Exception,
                        ExecutionResults = result.ExecutionResults,
                    };
                }

                return result;
            }
        }

        [Fact]
        public async Task Mismatch_PolicyRewindAndRetry_JournalUsed_FreshCalculatorMatches()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;

            Assert.Contains(HiveTestdataFixture.Chain, b => (ulong)b.Header.BlockNumber == targetMismatchBlock);

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;

            LyingOnceExecutor lyingExecutor = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    if (lyingExecutor == null)
                    {
                        lyingExecutor = new LyingOnceExecutor(realExecutor, targetMismatchBlock);
                        return lyingExecutor;
                    }
                    return realExecutor;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(1UL, result.RewindCyclesUsed);
            Assert.Equal(1, lyingExecutor!.TamperedCalls);
            Assert.Equal(expectedBlocks + 1UL, result.BlocksExecuted);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(lastChainBlock, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Mismatch_PolicyFatal_ReturnsFatalVerdict_NoStateCorruption()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;
            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.Fatal };
            LyingOnceExecutor lyingExecutor = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    lyingExecutor ??= new LyingOnceExecutor(realExecutor, targetMismatchBlock);
                    return lyingExecutor;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(targetMismatchBlock - 1, result.LastExecutedBlock);
            Assert.Equal(targetMismatchBlock - 1, result.BlocksExecuted);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(0UL, result.RewindCyclesUsed);
            Assert.Equal(1, lyingExecutor!.TamperedCalls);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(targetMismatchBlock - 1, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Mismatch_PolicyContinue_DoesNotCommitTamperedBlock()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;
            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.Continue };
            ulong expectedBlocks = (ulong)HiveTestdataFixture.Chain.Count;
            ulong lastChainBlock = (ulong)HiveTestdataFixture.Chain[HiveTestdataFixture.Chain.Count - 1].Header.BlockNumber;
            LyingOnceExecutor lyingExecutor = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    lyingExecutor ??= new LyingOnceExecutor(realExecutor, targetMismatchBlock);
                    return lyingExecutor;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(0UL, result.RewindCyclesUsed);
            Assert.Equal(1, lyingExecutor!.TamperedCalls);
            Assert.Equal(expectedBlocks - 1UL, result.BlocksExecuted);
            Assert.Equal(lastChainBlock, result.LastExecutedBlock);
        }

        [Fact]
        public async Task Mismatch_NoJournalNoSnapshot_ReturnsRewindUnavailable()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong targetMismatchBlock = 10UL;
            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            PredicateTamperExecutor tamper = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    tamper ??= new PredicateTamperExecutor(realExecutor, bn => bn == targetMismatchBlock);
                    return tamper;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.RewindUnavailable, result.ExitReason);
            Assert.Equal(targetMismatchBlock - 1, result.LastExecutedBlock);
            Assert.Equal(targetMismatchBlock - 1, result.BlocksExecuted);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(1UL, result.RewindCyclesUsed);
            Assert.Equal(1, tamper!.TamperedCalls);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            Assert.Equal(targetMismatchBlock - 1, reopened.Metadata.GetLastBlock());
        }

        [Fact]
        public async Task Mismatch_MaxConsecutiveDivergencesExceeded_ReturnsFatalVerdict()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong tamperStart = 10UL;
            const int maxConsecutive = 3;
            const int totalTampers = maxConsecutive + 1;
            ulong tamperEnd = tamperStart + (ulong)totalTampers - 1;

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.Continue };
            PredicateTamperExecutor tamper = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    tamper ??= new PredicateTamperExecutor(
                        realExecutor,
                        bn => bn >= tamperStart && bn <= tamperEnd);
                    return tamper;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(
                    StartBlock: 1,
                    CheckpointEvery: 0,
                    AnchorEvery: 0,
                    MaxConsecutiveDivergences: maxConsecutive),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(tamperStart - 1, result.LastExecutedBlock);
            Assert.Equal((ulong)totalTampers, result.RootMismatches);
            Assert.Equal(totalTampers, tamper!.TamperedCalls);
            Assert.Equal(0UL, result.RewindCyclesUsed);
            Assert.Contains("max consecutive divergences", result.Detail, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Rewind_SnapshotUsed_ReturnsSnapshotRestoreRequested()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong checkpointEvery = 5UL;
            const ulong tamperBlock = 7UL;
            ulong expectedSnapshotBlock = (tamperBlock - 1UL) / checkpointEvery * checkpointEvery;

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };
            PredicateTamperExecutor tamper = null;

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null),
                executorFactory: bundle =>
                {
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    tamper ??= new PredicateTamperExecutor(realExecutor, bn => bn == tamperBlock);
                    return tamper;
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: checkpointEvery, AnchorEvery: 0),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.SnapshotRestoreRequested, result.ExitReason);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Equal(1UL, result.RewindCyclesUsed);
            Assert.Equal(1, tamper!.TamperedCalls);
            Assert.NotNull(result.SnapshotRestoreTarget);
            Assert.Equal(expectedSnapshotBlock, result.SnapshotRestoreTarget!.Value.BlockNumber);

            _activeBundle?.Dispose(); _activeBundle = null;
            using var reopened = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: null);
            var snapshotDir = reopened.ResolveCheckpointSnapshotPath(expectedSnapshotBlock);
            Assert.True(Directory.Exists(snapshotDir),
                $"Snapshot dir for block {expectedSnapshotBlock} must remain on disk for the caller to consume");
        }

        [Fact]
        public async Task Rewind_MaxRewindCyclesExceeded_ReturnsFatalVerdict()
        {
            if (!HiveTestdataFixture.IsAvailable) return;

            using (var seedBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default))
            {
                await HiveTestdataFixture.PopulateGenesisAsync(seedBundle.State);
            }

            const ulong tamperBlock = 10UL;
            const int maxRewindCycles = 3;
            int factoryInvocations = 0;

            var source = new HiveChainRlpBlockSource(HiveTestdataFixture.Chain);
            var follower = new FollowerService();
            var policy = new FixedPolicy { Verdict = ValidationAction.RewindAndRetry };

            var result = await follower.RunAsync(
                source,
                bundleFactory: () => _activeBundle = RocksDbChainStoreBundle.Open(_dataDir, journalOptions: HistoricalStateOptions.Default),
                executorFactory: bundle =>
                {
                    factoryInvocations++;
                    var realExecutor = FollowerStackBuilder.Build(
                        bundle,
                        HiveTestdataFixture.ChainActivations,
                        HiveTestdataFixture.HardforkConfigFactory,
                        HiveTestdataFixture.ChainConfigFactory);
                    return new PredicateTamperExecutor(realExecutor, bn => bn == tamperBlock);
                },
                policy: policy,
                canonical: null,
                options: new FollowerOptions(
                    StartBlock: 1,
                    CheckpointEvery: 0,
                    AnchorEvery: 0,
                    MaxConsecutiveDivergences: maxRewindCycles + 10,
                    MaxRewindCycles: maxRewindCycles),
                ct: CancellationToken.None);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Contains("MaxRewindCycles", result.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Equal((ulong)(maxRewindCycles + 1), result.RewindCyclesUsed);
            Assert.Equal((ulong)(maxRewindCycles + 1), result.RootMismatches);
            Assert.Equal(maxRewindCycles + 1, factoryInvocations);
            Assert.Equal(tamperBlock - 1, result.LastExecutedBlock);
        }
    }
}
