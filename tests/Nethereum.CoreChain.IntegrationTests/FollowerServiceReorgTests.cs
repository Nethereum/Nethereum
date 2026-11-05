using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests
{
    public sealed class FollowerServiceReorgTests
    {
        private const string MarkerAccount = "0x00000000000000000000000000000000000000aa";

        private static byte[] Fill(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static byte[] HashOf(ulong block, int generation)
        {
            var hash = new byte[32];
            hash[0] = (byte)(generation + 1);
            for (var i = 0; i < 8; i++) hash[1 + i] = (byte)(block >> (8 * (7 - i)));
            return hash;
        }

        private static byte[] StateRootOf(ulong block, int generation)
        {
            var root = HashOf(block, generation);
            root[31] = 0x5A;
            return root;
        }

        private static async Task SeedAsync(IChainStoreBundle bundle, ulong from, ulong to, Func<ulong, int> generationOf)
        {
            for (var n = from; n <= to; n++)
            {
                var generation = generationOf(n);
                var header = new BlockHeader
                {
                    BlockNumber = n,
                    StateRoot = StateRootOf(n, generation),
                    ParentHash = n == 0 ? new byte[32] : HashOf(n - 1, generationOf(n - 1)),
                };
                await bundle.Blocks.SaveAsync(header, HashOf(n, generation));
                await bundle.Uncles.SaveAsync(HashOf(n, generation), new List<BlockHeader>());
            }
        }

        private static async Task SeedJournalAsync(IChainStoreBundle bundle, ulong from, ulong to)
        {
            for (var n = from; n <= to; n++)
                await bundle.Diffs.SaveBlockDiffAsync(new BlockStateDiff { BlockNumber = n });
        }

        private static FollowerOptions Options(ulong endBlock, bool externalHeaderFollow)
            => new FollowerOptions(
                StartBlock: 0,
                CheckpointEvery: 0,
                AnchorEvery: 0,
                EndBlock: endBlock,
                TipPollInterval: TimeSpan.FromMilliseconds(10),
                WalkerInvocationThreshold: 1UL)
            { ExternalHeaderFollow = externalHeaderFollow };

        private sealed class ScriptedTips : ICanonicalStateRootSource
        {
            private readonly Queue<CanonicalTip> _tips;
            public string Name => "scripted";

            public ScriptedTips(IEnumerable<CanonicalTip> tips) => _tips = new Queue<CanonicalTip>(tips);

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[], byte[])>((null!, null!));

            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
                => Task.FromResult(_tips.Count == 0 ? null! : _tips.Dequeue());
        }

        private sealed class HookedExecutor : IBlockExecutor
        {
            private readonly Func<ulong, Task> _afterBlock;
            private readonly Func<ulong, bool> _mismatchAt;

            public HookedExecutor(Func<ulong, Task> afterBlock = null, Func<ulong, bool> mismatchAt = null)
            {
                _afterBlock = afterBlock;
                _mismatchAt = mismatchAt;
            }

            public List<(ulong Number, byte[] ParentHash)> Executed { get; } = new();

            public async Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
            {
                var number = (ulong)header.BlockNumber;
                Executed.Add((number, header.ParentHash));
                if (_mismatchAt != null && _mismatchAt(number))
                    return new BlockImporterResult
                    {
                        ComputedStateRoot = Fill(0xEE),
                        ExpectedStateRoot = header.StateRoot,
                        StateRootMismatch = true,
                    };
                var result = new BlockImporterResult
                {
                    ComputedStateRoot = header.StateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = false,
                };
                if (_afterBlock != null) await _afterBlock(number);
                return result;
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
                => Entries.Enqueue((logLevel, formatter(state, exception)));

            public string Dump() => string.Join(Environment.NewLine, Entries.Select(e => e.Level + " " + e.Message));

            public bool Has(LogLevel level, string fragment)
                => Entries.Any(e => e.Level == level && e.Message.Contains(fragment));
        }

        private sealed class NoopPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private static BackwardWalkerDelegate NeverWalks()
            => (fromBlock, fromHash, toBlock, bundle, ct, noShortCircuitAboveBlock)
                => throw new InvalidOperationException("the external-header-follow path must not walk");

        private static Task<FollowerRunResult> RunAsync(
            FollowerService follower, IChainStoreBundle bundle, IBlockExecutor executor,
            ICanonicalStateRootSource canonical, FollowerOptions options, ILogger logger)
            => follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: options,
                ct: new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token,
                logger: logger);

        private sealed class CheckpointDirBundle : IChainStoreBundle, IPromotionFloorGuard
        {
            private readonly IChainStoreBundle _inner;
            private readonly string _snapshotDir;
            private readonly ulong? _promotionFloor;

            public CheckpointDirBundle(IChainStoreBundle inner, string snapshotDir, ulong? promotionFloor)
            {
                _inner = inner;
                _snapshotDir = snapshotDir;
                _promotionFloor = promotionFloor;
            }

            public ulong? PromotionFloor(ulong currentHead) => _promotionFloor;
            public string ResolveCheckpointSnapshotPath(ulong blockNumber) => _snapshotDir;

            public IStateStore State => _inner.State;
            public ITrieNodeStore TrieNodes => _inner.TrieNodes;
            public ITrieNodeStore StateTrieNodes => _inner.StateTrieNodes;
            public NodeCommitBlockContext NodeCommitBlockSource => _inner.NodeCommitBlockSource;
            public IBlockStore Blocks => _inner.Blocks;
            public ITransactionStore Transactions => _inner.Transactions;
            public IUncleStore Uncles => _inner.Uncles;
            public IWithdrawalStore Withdrawals => _inner.Withdrawals;
            public IBlockAccessListStore BlockAccessLists => _inner.BlockAccessLists;
            public IReceiptStore Receipts => _inner.Receipts;
            public ILogStore Logs => _inner.Logs;
            public IChainMetadataStore Metadata => _inner.Metadata;
            public IStateDiffStore Diffs => _inner.Diffs;
            public bool JournalEnabled => _inner.JournalEnabled;
            public long FreezerHead => _inner.FreezerHead;
            public long ByHashIndexedHead => _inner.ByHashIndexedHead;
            public long LogIndexRenderedHead => _inner.LogIndexRenderedHead;
            public long LogRenderProgressBlock => _inner.LogRenderProgressBlock;

            public Task<ChainCheckpoint> SaveCheckpointAsync(ulong blockNumber, byte[] stateRoot, byte[] blockHash, CancellationToken ct = default)
                => _inner.SaveCheckpointAsync(blockNumber, stateRoot, blockHash, ct);
            public Task<IReadOnlyList<ChainCheckpoint>> ListCheckpointsAsync(CancellationToken ct = default)
                => _inner.ListCheckpointsAsync(ct);
            public Task RestoreCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
                => _inner.RestoreCheckpointAsync(blockNumber, ct);
            public Task DeleteCheckpointAsync(ulong blockNumber, CancellationToken ct = default)
                => _inner.DeleteCheckpointAsync(blockNumber, ct);
            public Task ResetStateOnlyAsync(CancellationToken ct = default) => _inner.ResetStateOnlyAsync(ct);
            public Task ResetSnapBootstrapStateAsync(CancellationToken ct = default) => _inner.ResetSnapBootstrapStateAsync(ct);
            public Task ExportDatabaseAsync(string outputPath, CancellationToken ct = default) => _inner.ExportDatabaseAsync(outputPath, ct);
            public IBundleBatch BeginBatch() => _inner.BeginBatch();
            public void Dispose() => _inner.Dispose();
            public ValueTask DisposeAsync() => _inner.DisposeAsync();
        }

        private sealed class Scenario
        {
            public InMemoryChainStoreBundle Bundle { get; init; }
            public HookedExecutor Executor { get; init; }
            public CapturingLogger Logger { get; } = new CapturingLogger();
            public ScriptedTips Tips { get; init; }
            public List<ulong> Requeues { get; } = new List<ulong>();

            public BodyRepairDelegate Requeue => (from, to, b, ct) =>
            {
                Requeues.Add(from);
                return Task.FromResult(true);
            };
        }

        private static async Task<Scenario> ScenarioAsync(int reorgDepth)
        {
            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SeedAsync(bundle, 0, 300, _ => 0);
            bundle.Metadata.Commit(0UL, HashOf(0, 0));
            await SeedJournalAsync(bundle, 173, 300);
            var ancestor = (ulong)(300 - reorgDepth);
            var relaid = false;
            var executor = new HookedExecutor(async n =>
            {
                if (n != 300 || relaid) return;
                relaid = true;
                await SeedAsync(bundle, ancestor + 1, 301, g => g > ancestor ? 1 : 0);
            });
            var tips = new ScriptedTips(new[]
            {
                new CanonicalTip { BlockNumber = 300, BlockHash = HashOf(300, 0) },
                new CanonicalTip { BlockNumber = 301, BlockHash = HashOf(301, 1) },
                new CanonicalTip { BlockNumber = 301, BlockHash = HashOf(301, 1) },
                new CanonicalTip { BlockNumber = 301, BlockHash = HashOf(301, 1) },
            });
            return new Scenario { Bundle = bundle, Executor = executor, Tips = tips };
        }

        [Fact]
        public async Task W2_T1_Given_ATipFollower300BlocksPastItsLastCheckpoint_When_TheClRelaysTheLast2Blocks_Then_ItRewindsExactly2ThroughTheJournalAndReExecutesTheNew2()
        {
            var s = await ScenarioAsync(reorgDepth: 2);
            s.Bundle.Metadata.SaveCheckpoint(1, StateRootOf(1, 0), HashOf(1, 0));
            var follower = new FollowerService(NeverWalks(), null, s.Requeue);

            var result = await RunAsync(follower, s.Bundle, s.Executor, s.Tips, Options(301, externalHeaderFollow: true), s.Logger);

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail + Environment.NewLine + s.Logger.Dump());
            Assert.Null(result.SnapshotRestoreTarget);
            Assert.Equal(301UL, result.LastExecutedBlock);
            Assert.Equal(303, s.Executor.Executed.Count);
            Assert.Equal(new ulong[] { 299, 300, 301 }, s.Executor.Executed.Skip(300).Select(e => e.Number).ToArray());
            Assert.Equal(HashOf(298, 0), s.Executor.Executed[300].ParentHash);
            Assert.Equal(HashOf(299, 1), s.Executor.Executed[301].ParentHash);
            Assert.True(s.Logger.Has(LogLevel.Information, "undone=2"));
            Assert.Contains(299UL, s.Requeues);
            Assert.False(s.Logger.Has(LogLevel.Error, "reorg.disaster_recovery"));
        }

        [Fact]
        public async Task W2_T2_Given_AReorgWhoseAncestorIsBeyondTheJournal_When_TheClRelaysIt_Then_TheRewindFallsBackToTheNewestCheckpointAtOrBelowTheAncestorAndLogsDisasterRecovery()
        {
            var s = await ScenarioAsync(reorgDepth: 150);
            s.Bundle.Metadata.SaveCheckpoint(100, StateRootOf(100, 0), HashOf(100, 0));
            s.Bundle.Metadata.SaveCheckpoint(200, StateRootOf(200, 0), HashOf(200, 0));
            var snapshotDir = Path.Combine(Path.GetTempPath(), "w2-t2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDir);
            try
            {
                var bundle = new CheckpointDirBundle(s.Bundle, snapshotDir, promotionFloor: null);
                var follower = new FollowerService(NeverWalks(), null, s.Requeue);

                var result = await RunAsync(follower, bundle, s.Executor, s.Tips, Options(301, externalHeaderFollow: true), s.Logger);

                Assert.Equal(FollowerExitReason.SnapshotRestoreRequested, result.ExitReason);
                Assert.Equal(100UL, result.SnapshotRestoreTarget!.Value.BlockNumber);
                Assert.True(s.Logger.Has(LogLevel.Error, "reorg.disaster_recovery"));
            }
            finally
            {
                Directory.Delete(snapshotDir, recursive: true);
            }
        }

        [Fact]
        public async Task W2_T2_Given_AReorgWhoseRewindTargetIsBelowThePromotionFloor_When_TheClRelaysIt_Then_TheRewindIsRefusedAndNothingIsReExecuted()
        {
            var s = await ScenarioAsync(reorgDepth: 150);
            s.Bundle.Metadata.SaveCheckpoint(100, StateRootOf(100, 0), HashOf(100, 0));
            var snapshotDir = Path.Combine(Path.GetTempPath(), "w2-t2f-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDir);
            try
            {
                var bundle = new CheckpointDirBundle(s.Bundle, snapshotDir, promotionFloor: 200);
                var follower = new FollowerService(NeverWalks(), null, s.Requeue);

                var result = await RunAsync(follower, bundle, s.Executor, s.Tips, Options(301, externalHeaderFollow: true), s.Logger);

                Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
                Assert.Contains("promotion floor", result.Detail);
                Assert.Equal(300, s.Executor.Executed.Count);
            }
            finally
            {
                Directory.Delete(snapshotDir, recursive: true);
            }
        }

        [Fact]
        public async Task W2_C4_Given_AReorgDeeperThanTheExecutedHashWindow_When_NoCommonAncestorIsRecorded_Then_ItTakesTheLoggedDisasterRecoveryPathInsteadOfStoppingTheNode()
        {
            var s = await ScenarioAsync(reorgDepth: 250);
            s.Bundle.Metadata.SaveCheckpoint(100, StateRootOf(100, 0), HashOf(100, 0));
            var snapshotDir = Path.Combine(Path.GetTempPath(), "w2-c4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDir);
            try
            {
                var bundle = new CheckpointDirBundle(s.Bundle, snapshotDir, promotionFloor: null);
                var follower = new FollowerService(NeverWalks(), null, s.Requeue);

                var result = await RunAsync(
                    follower, bundle, s.Executor, s.Tips, Options(301, externalHeaderFollow: true) with { MaxReorgDepth = 200 }, s.Logger);

                Assert.Equal(FollowerExitReason.SnapshotRestoreRequested, result.ExitReason);
                Assert.Equal(100UL, result.SnapshotRestoreTarget!.Value.BlockNumber);
                Assert.True(s.Logger.Has(LogLevel.Error, "reorg.disaster_recovery"));
            }
            finally
            {
                Directory.Delete(snapshotDir, recursive: true);
            }
        }

        [Fact]
        public async Task W2_T3_Given_AWalkerDivergenceRepairWithExecutedStateAboveTheAncestor_When_TheHeadMovesToTheAncestor_Then_TheStateIsRewoundToTheAncestor()
        {
            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SeedAsync(bundle, 1, 80, _ => 0);
            bundle.Metadata.Commit(80UL, HashOf(80, 0));
            await bundle.State.SaveAccountAsync(MarkerAccount, new Account { Balance = 80 });
            for (ulong n = 61; n <= 80; n++)
                await bundle.Diffs.SaveBlockDiffAsync(new BlockStateDiff
                {
                    BlockNumber = n,
                    AccountDiffs = { new AccountDiffEntry { Address = MarkerAccount, PreValue = new Account { Balance = n - 1 } } },
                });

            var executor = new HookedExecutor();
            var tips = new ScriptedTips(Enumerable.Repeat(new CanonicalTip { BlockNumber = 81, BlockHash = HashOf(81, 0) }, 4));
            var walkerCalls = 0;
            BackwardWalkerDelegate walker = async (fromBlock, fromHash, toBlock, b, ct, noShortCircuitAboveBlock) =>
            {
                walkerCalls++;
                if (walkerCalls == 1)
                    return new WalkerOutcome(WalkerExitReason.LastKnownGoodDivergence, HeadersWritten: 0, DivergenceBlock: 70UL);
                await SeedAsync(b, b.Metadata.GetLastBlock() + 1, 81, _ => 0);
                return new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null);
            };
            AncestorResolverDelegate resolver = (diverged, floor, ct) => Task.FromResult(60UL);
            var follower = new FollowerService(walker, resolver);

            var result = await RunAsync(follower, bundle, executor, tips, Options(81, externalHeaderFollow: false), new CapturingLogger());

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(61UL, executor.Executed[0].Number);
            Assert.Equal((BigInteger)60, (BigInteger)(await bundle.State.GetAccountAsync(MarkerAccount)).Balance);
        }

        [Fact]
        public async Task W2_T5_Given_ARootMismatchOnTheTipPathWithNoAncestorResolver_When_TheFollowerRewinds_Then_ItStepsBackExactlyOneBlock()
        {
            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            bundle.Metadata.Commit(50UL, HashOf(50, 0));
            await SeedAsync(bundle, 1, 51, _ => 0);
            await SeedJournalAsync(bundle, 40, 51);
            var mismatchedOnce = false;
            var executor = new HookedExecutor(mismatchAt: n =>
            {
                if (n != 51 || mismatchedOnce) return false;
                mismatchedOnce = true;
                return true;
            });
            var log = new CapturingLogger();
            var tips = new ScriptedTips(Enumerable.Repeat(new CanonicalTip { BlockNumber = 51, BlockHash = HashOf(51, 0) }, 6));
            BackwardWalkerDelegate walker = async (f, h, t, b, ct, n) =>
            {
                await SeedAsync(b, b.Metadata.GetLastBlock() + 1, 51, _ => 0);
                return new WalkerOutcome(WalkerExitReason.ReachedTarget, HeadersWritten: 0, DivergenceBlock: null);
            };

            var result = await RunAsync(new FollowerService(walker), bundle, executor, tips, Options(51, externalHeaderFollow: false), log);

            Assert.True(result.ExitReason == FollowerExitReason.SourceCompleted, result.ExitReason + " " + result.Detail + Environment.NewLine + log.Dump());
            Assert.Equal(new ulong[] { 51, 50, 51 }, executor.Executed.Select(e => e.Number).ToArray());
            Assert.Equal(1UL, result.RootMismatches);
        }

        [Fact]
        public async Task W2_T5_Given_ARootMismatchOnTheTipPathWithAnAncestorResolver_When_TheFollowerRewinds_Then_ItRewindsToTheResolvedAncestor()
        {
            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            bundle.Metadata.Commit(50UL, HashOf(50, 0));
            await SeedAsync(bundle, 1, 51, _ => 0);
            await SeedJournalAsync(bundle, 40, 51);
            var mismatchedOnce = false;
            var executor = new HookedExecutor(mismatchAt: n =>
            {
                if (n != 51 || mismatchedOnce) return false;
                mismatchedOnce = true;
                return true;
            });
            var tips = new ScriptedTips(Enumerable.Repeat(new CanonicalTip { BlockNumber = 51, BlockHash = HashOf(51, 0) }, 6));
            BackwardWalkerDelegate walker = async (f, h, t, b, ct, n) =>
            {
                await SeedAsync(b, b.Metadata.GetLastBlock() + 1, 51, _ => 0);
                return new WalkerOutcome(WalkerExitReason.ReachedTarget, HeadersWritten: 0, DivergenceBlock: null);
            };
            AncestorResolverDelegate resolver = (diverged, floor, ct) => Task.FromResult(45UL);

            var result = await RunAsync(new FollowerService(walker, resolver), bundle, executor, tips, Options(51, externalHeaderFollow: false), new CapturingLogger());

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(new ulong[] { 51, 46, 47, 48, 49, 50, 51 }, executor.Executed.Select(e => e.Number).ToArray());
        }
    }
}
