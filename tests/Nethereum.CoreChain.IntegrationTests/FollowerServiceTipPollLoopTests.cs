using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests
{
    /// <summary>
    /// W-1 + W-2 wiring tests — tip-event-driven main loop in FollowerService
    /// driven by ICanonicalStateRootSource polling and BackwardWalkerDelegate.
    /// </summary>
    public sealed class FollowerServiceTipPollLoopTests
    {
        private static byte[] Fill(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static byte[] HashFor(ulong block) => Fill((byte)(block + 0x80));
        private static byte[] StateRootFor(ulong block) => Fill((byte)block);

        private sealed class FakeCanonicalSource : ICanonicalStateRootSource
        {
            private readonly Queue<CanonicalTip?> _tips;
            public string Name => "fake";
            public int GetLatestCalls;

            public FakeCanonicalSource(IEnumerable<CanonicalTip?> tips)
            {
                _tips = new Queue<CanonicalTip?>(tips);
            }

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[], byte[])>((null!, null!));

            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct)
            {
                Interlocked.Increment(ref GetLatestCalls);
                if (_tips.Count == 0) return Task.FromResult<CanonicalTip>(null!);
                var t = _tips.Dequeue();
                return Task.FromResult(t!);
            }
        }

        private sealed class FakeWalker
        {
            private readonly Queue<WalkerOutcome> _outcomes;
            public List<(ulong From, ulong To)> Calls { get; } = new();
            public Func<(ulong fromBlock, ulong toBlock, IChainStoreBundle bundle), Task>? SideEffect { get; set; }

            public FakeWalker(IEnumerable<WalkerOutcome> outcomes)
            {
                _outcomes = new Queue<WalkerOutcome>(outcomes);
            }

            public BackwardWalkerDelegate AsDelegate() => async (fromBlock, fromHash, toBlock, bundle, ct) =>
            {
                Calls.Add((fromBlock, toBlock));
                if (SideEffect != null) await SideEffect((fromBlock, toBlock, bundle));
                if (_outcomes.Count == 0)
                {
                    return new WalkerOutcome(WalkerExitReason.ReachedTarget, HeadersWritten: 0, DivergenceBlock: null);
                }
                return _outcomes.Dequeue();
            };
        }

        private sealed class ScriptedExecutor : IBlockExecutor
        {
            public int CallCount;

            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header,
                IList<ISignedTransaction> transactions,
                IList<BlockHeader> uncles,
                IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                Interlocked.Increment(ref CallCount);
                return Task.FromResult(new BlockImporterResult
                {
                    ComputedStateRoot = header.StateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = false,
                });
            }
        }

        private sealed class NoopPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private static async Task SeedBlocksAsync(IChainStoreBundle bundle, ulong from, ulong to)
        {
            for (ulong n = from; n <= to; n++)
            {
                var header = new BlockHeader { BlockNumber = n, StateRoot = StateRootFor(n) };
                var hash = HashFor(n);
                await bundle.Blocks.SaveAsync(header, hash);
                await bundle.Uncles.SaveAsync(hash, new List<BlockHeader>());
            }
        }

        private static FollowerOptions BuildOptions(ulong startBlock = 0, ulong? endBlock = null, int walkerThreshold = 1)
            => new FollowerOptions(
                StartBlock: startBlock,
                CheckpointEvery: 0,
                AnchorEvery: 0,
                EndBlock: endBlock,
                TipPollInterval: TimeSpan.FromMilliseconds(10),
                WalkerInvocationThreshold: (ulong)walkerThreshold);

        [Fact]
        public async Task InitialSync_NoLocalState_WalkerInvokedToGenesis()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(new[]
            {
                new CanonicalTip { BlockNumber = 5, BlockHash = HashFor(5) },
            });

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.StructuralGenesis, HeadersWritten: 6, DivergenceBlock: null),
            });
            walker.SideEffect = async args => await SeedBlocksAsync(args.bundle, 1, 5);

            var follower = new FollowerService(walker.AsDelegate());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var options = BuildOptions(endBlock: 5);

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: options,
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Single(walker.Calls);
            Assert.Equal(5UL, walker.Calls[0].From);
            Assert.Equal(0UL, walker.Calls[0].To);
            Assert.Equal(5UL, result.LastExecutedBlock);
            Assert.Equal(5, executor.CallCount);
        }

        [Fact]
        public async Task SteadyState_TipAdvances_WalkerInvokedPerTipUpdate()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(100UL, HashFor(100));
            await SeedBlocksAsync(bundle, 1, 100);

            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(new[]
            {
                new CanonicalTip { BlockNumber = 101, BlockHash = HashFor(101) },
                new CanonicalTip { BlockNumber = 102, BlockHash = HashFor(102) },
                new CanonicalTip { BlockNumber = 103, BlockHash = HashFor(103) },
            });

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
            });
            ulong nextSeed = 101;
            walker.SideEffect = async args =>
            {
                await SeedBlocksAsync(args.bundle, nextSeed, nextSeed);
                nextSeed++;
            };

            var follower = new FollowerService(walker.AsDelegate());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(endBlock: 103),
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(3, walker.Calls.Count);
            Assert.Equal(101UL, walker.Calls[0].From);
            Assert.Equal(102UL, walker.Calls[1].From);
            Assert.Equal(103UL, walker.Calls[2].From);
            Assert.Equal(103UL, result.LastExecutedBlock);
        }

        [Fact]
        public async Task TipUnchanged_NoWalkerInvocation()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(100UL, HashFor(100));
            await SeedBlocksAsync(bundle, 1, 100);

            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(new[]
            {
                new CanonicalTip { BlockNumber = 101, BlockHash = HashFor(101) },
                new CanonicalTip { BlockNumber = 101, BlockHash = HashFor(101) },
                new CanonicalTip { BlockNumber = 101, BlockHash = HashFor(101) },
            });

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
            });
            walker.SideEffect = async args => await SeedBlocksAsync(args.bundle, 101, 101);

            var follower = new FollowerService(walker.AsDelegate());
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(endBlock: 101),
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Single(walker.Calls);
            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(101UL, result.LastExecutedBlock);
        }

        [Fact]
        public async Task Divergence_LogsAndHalts()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(50UL, HashFor(50));
            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(new[]
            {
                new CanonicalTip { BlockNumber = 100, BlockHash = HashFor(100) },
            });

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.LastKnownGoodDivergence, HeadersWritten: 0, DivergenceBlock: 75UL),
            });

            var follower = new FollowerService(walker.AsDelegate());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(),
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Contains("divergence", result.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("75", result.Detail);
            Assert.Single(walker.Calls);
        }

        [Fact]
        public async Task PeerPoolEmpty_BackoffAndRetry()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(50UL, HashFor(50));
            await SeedBlocksAsync(bundle, 1, 50);

            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(Enumerable.Repeat(
                new CanonicalTip { BlockNumber = 51, BlockHash = HashFor(51) }, 10));

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.PeerPoolEmpty, HeadersWritten: 0, DivergenceBlock: null),
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
            });
            int call = 0;
            walker.SideEffect = async args =>
            {
                call++;
                if (call >= 2) await SeedBlocksAsync(args.bundle, 51, 51);
            };

            var follower = new FollowerService(walker.AsDelegate());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(endBlock: 51),
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.True(walker.Calls.Count >= 2, $"expected >=2 walker invocations, got {walker.Calls.Count}");
            Assert.Equal(51UL, result.LastExecutedBlock);
        }

        [Fact]
        public async Task Divergence_FindsAncestor_RewindsCleanly()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(50UL, HashFor(50));
            await SeedBlocksAsync(bundle, 1, 50);

            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(new[]
            {
                new CanonicalTip { BlockNumber = 80, BlockHash = HashFor(80) },
                new CanonicalTip { BlockNumber = 80, BlockHash = HashFor(80) },
            });

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.LastKnownGoodDivergence, HeadersWritten: 0, DivergenceBlock: 60UL),
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
            });
            int callCount = 0;
            walker.SideEffect = async args =>
            {
                callCount++;
                if (callCount >= 2)
                {
                    var cursor = bundle.Metadata.GetLastBlock();
                    await SeedBlocksAsync(args.bundle, cursor + 1, 80);
                }
            };

            AncestorResolverDelegate ancestorResolver = (diverged, floor, ct) =>
                Task.FromResult(40UL);

            var follower = new FollowerService(walker.AsDelegate(), ancestorResolver);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(endBlock: 80),
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.True(walker.Calls.Count >= 2, $"expected >=2 walker invocations, got {walker.Calls.Count}");
            Assert.Equal(80UL, result.LastExecutedBlock);
            Assert.Equal(40UL, bundle.Metadata.GetLastFetchedHeader());
        }

        [Fact]
        public async Task Cancellation_FollowerExitsCleanly()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var executor = new ScriptedExecutor();
            var canonical = new FakeCanonicalSource(Enumerable.Range(0, 1000)
                .Select(_ => (CanonicalTip?)null));

            var walker = new FakeWalker(Array.Empty<WalkerOutcome>());
            var follower = new FollowerService(walker.AsDelegate());

            using var cts = new CancellationTokenSource();
            var runTask = follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(),
                ct: cts.Token,
                logger: NullLogger.Instance);

            await Task.Delay(50);
            cts.Cancel();

            var result = await runTask;
            Assert.Equal(FollowerExitReason.Cancelled, result.ExitReason);
            Assert.Empty(walker.Calls);
        }

        [Fact]
        public async Task PostShanghaiBlock_WithdrawalsFlowFromBundle_ThroughOrderingBlockSource_ToExecutor()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(100UL, HashFor(100));
            await SeedBlocksAsync(bundle, 1, 100);

            var withdrawalsBlock101 = new List<Withdrawal>
            {
                new Withdrawal { Index = 7001UL, ValidatorIndex = 42UL, Address = new byte[20], AmountInGwei = 32_000_000UL },
                new Withdrawal { Index = 7002UL, ValidatorIndex = 43UL, Address = new byte[20], AmountInGwei = 16_500_000UL },
            };

            var executor = new RecordingExecutor();
            var canonical = new FakeCanonicalSource(new[]
            {
                new CanonicalTip { BlockNumber = 101, BlockHash = HashFor(101) },
            });

            var walker = new FakeWalker(new[]
            {
                new WalkerOutcome(WalkerExitReason.MetExistingStore, HeadersWritten: 1, DivergenceBlock: null),
            });
            walker.SideEffect = async args =>
            {
                await SeedBlocksAsync(args.bundle, 101, 101);
                await args.bundle.Withdrawals.SaveAsync(HashFor(101), withdrawalsBlock101);
            };

            var follower = new FollowerService(walker.AsDelegate());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await follower.RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                bundleFactory: () => bundle,
                executorFactory: _ => executor,
                policy: new NoopPolicy(),
                canonical: canonical,
                options: BuildOptions(endBlock: 101),
                ct: cts.Token,
                logger: NullLogger.Instance);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(101UL, result.LastExecutedBlock);

            var seen = executor.LastWithdrawals;
            Assert.NotNull(seen);
            Assert.Equal(2, seen.Count);
            Assert.Equal(32_000_000L, (long)seen[0].AmountGwei);
            Assert.Equal(16_500_000L, (long)seen[1].AmountGwei);
        }

        private sealed class RecordingExecutor : IBlockExecutor
        {
            public IList<WithdrawalEntry>? LastWithdrawals;

            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header,
                IList<ISignedTransaction> transactions,
                IList<BlockHeader> uncles,
                IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                LastWithdrawals = withdrawals;
                return Task.FromResult(new BlockImporterResult
                {
                    ComputedStateRoot = header.StateRoot,
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = false,
                });
            }
        }
    }
}
