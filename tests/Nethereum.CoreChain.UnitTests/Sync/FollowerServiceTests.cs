using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class FollowerServiceTests
    {
        private static byte[] FillBytes(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static BlockBundle MakeBundle(ulong blockNumber)
            => new BlockBundle(
                Header: new BlockHeader { BlockNumber = blockNumber, StateRoot = FillBytes((byte)blockNumber) },
                Transactions: new List<ISignedTransaction>(),
                Uncles: new List<BlockHeader>(),
                Withdrawals: null,
                HeaderHash: FillBytes((byte)(blockNumber + 0x80)));

        private sealed class ScriptedExecutor : IBlockExecutor
        {
            private readonly Dictionary<ulong, bool> _matchByBlock;
            public int CallCount { get; private set; }
            public ScriptedExecutor(Dictionary<ulong, bool> matchByBlock) { _matchByBlock = matchByBlock; }

            public Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header,
                IList<ISignedTransaction> transactions,
                IList<BlockHeader> uncles,
                IList<WithdrawalEntry> withdrawals,
                CancellationToken ct)
            {
                CallCount++;
                var bn = (ulong)header.BlockNumber;
                var match = !_matchByBlock.TryGetValue(bn, out var v) || v;
                return Task.FromResult(new BlockImporterResult
                {
                    ComputedStateRoot = match ? header.StateRoot : FillBytes(0xFF),
                    ExpectedStateRoot = header.StateRoot,
                    StateRootMismatch = !match,
                });
            }
        }

        private sealed class PolicyStub : IValidationPolicy
        {
            public bool AnchorAlways { get; set; }
            public ValidationAction VerdictAction { get; set; } = ValidationAction.Continue;
            public int OnVerdictCalls { get; private set; }
            public bool ShouldAnchorAt(ulong blockNumber) => AnchorAlways;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
            {
                OnVerdictCalls++;
                return VerdictAction;
            }
        }

        private static Func<IChainStoreBundle> BundleFactoryFor(IChainStoreBundle b, Action onCall = null)
            => () => { onCall?.Invoke(); return b; };

        [Fact]
        public async Task RunAsync_HappyPath_AdvancesCursor_CheckpointsAtIntervals()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var source = new LocalReplayBlockSource(new List<BlockBundle>
            {
                MakeBundle(1), MakeBundle(2), MakeBundle(3),
            });
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source,
                bundleFactory: BundleFactoryFor(bundle),
                executorFactory: _ => executor,
                policy: new PolicyStub(),
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 2, AnchorEvery: 0),
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(3UL, result.LastExecutedBlock);
            Assert.Equal(3UL, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(3, executor.CallCount);
            Assert.NotNull(bundle.Metadata.GetCheckpoint(2));
            Assert.Null(bundle.Metadata.GetCheckpoint(1));
            Assert.Null(bundle.Metadata.GetCheckpoint(3));
        }

        [Fact]
        public async Task RunAsync_SourceCompletes_ReturnsSourceCompleted()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var source = new LocalReplayBlockSource(new List<BlockBundle>());
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, new PolicyStub(), null,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 100, AnchorEvery: 0), default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(0UL, result.BlocksExecuted);
        }

        [Fact]
        public async Task RunAsync_PreCancelled_ReturnsCancelled()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var source = new LocalReplayBlockSource(new List<BlockBundle> { MakeBundle(1) });
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, new PolicyStub(), null,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0), cts.Token);

            Assert.Equal(FollowerExitReason.Cancelled, result.ExitReason);
        }

        [Fact]
        public async Task RunAsync_StateRootMismatch_PolicyFatal_ReturnsFatalVerdict()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var source = new LocalReplayBlockSource(new List<BlockBundle> { MakeBundle(1) });
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool> { { 1, false } });
            var policy = new PolicyStub { VerdictAction = ValidationAction.Fatal };
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, null,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0), default);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Contains("fatal verdict", result.Detail);
            Assert.Single(source.BadBundleReports);
        }

        [Fact]
        public async Task RunAsync_MaxConsecutiveDivergencesExceeded_ReturnsFatalVerdict()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var bundles = Enumerable.Range(1, 5).Select(i => MakeBundle((ulong)i)).ToList();
            var source = new LocalReplayBlockSource(bundles);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>
            {
                { 1, false }, { 2, false }, { 3, false }, { 4, false }, { 5, false },
            });
            var policy = new PolicyStub { VerdictAction = ValidationAction.Continue };
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, null,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0, MaxConsecutiveDivergences: 3), default);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Contains("max consecutive divergences", result.Detail);
        }

        [Fact]
        public async Task RunAsync_RewindAndRetry_NoJournalNoSnapshot_ReturnsRewindUnavailable()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var source = new LocalReplayBlockSource(new List<BlockBundle> { MakeBundle(1) });
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool> { { 1, false } });
            var policy = new PolicyStub { VerdictAction = ValidationAction.RewindAndRetry };
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, null,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0), default);

            Assert.Equal(FollowerExitReason.RewindUnavailable, result.ExitReason);
            Assert.Equal(1UL, result.RewindCyclesUsed);
        }

        private sealed class TransientFailingSource : IBlockSource
        {
            private readonly IList<BlockBundle> _bundles;
            private readonly int _failAfterYield;
            private int _callCount;

            public List<ulong> StreamStartCalls { get; } = new();
            public List<(ulong, BadBundleReason)> BadBundleReports { get; } = new();
            public DivergenceSignal LastChainBreak => null;

            public TransientFailingSource(IList<BlockBundle> bundles, int failAfterYield)
            {
                _bundles = bundles;
                _failAfterYield = failAfterYield;
            }

            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock,
                [EnumeratorCancellation] CancellationToken ct)
            {
                StreamStartCalls.Add(fromBlock);
                _callCount++;
                bool throwAfter = _callCount == 1;

                int yielded = 0;
                foreach (var b in _bundles)
                {
                    ct.ThrowIfCancellationRequested();
                    if ((ulong)b.Header.BlockNumber < fromBlock) continue;
                    if (throwAfter && yielded >= _failAfterYield)
                    {
                        throw new System.IO.IOException("simulated transient peer drop");
                    }
                    yield return b;
                    yielded++;
                    await Task.Yield();
                }
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct)
                => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
            {
                BadBundleReports.Add((blockNumber, reason));
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task RunAsync_SourceThrowsAfterCommits_RestartsAtNextUnexecutedBlock()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var bundles = Enumerable.Range(1, 5).Select(i => MakeBundle((ulong)i)).ToList();
            var source = new TransientFailingSource(bundles, failAfterYield: 3);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source,
                BundleFactoryFor(bundle),
                _ => executor,
                new PolicyStub(),
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0, MaxConsecutiveDivergences: 8),
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(5UL, result.LastExecutedBlock);
            Assert.Equal(5UL, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(2, source.StreamStartCalls.Count);
            Assert.Equal(1UL, source.StreamStartCalls[0]);
            Assert.Equal(4UL, source.StreamStartCalls[1]);
        }

        private sealed class StubCanonicalSource : ICanonicalStateRootSource
        {
            private readonly Dictionary<ulong, byte[]> _byBlock;
            private readonly bool _throwOnCall;

            public List<ulong> Queries { get; } = new();
            public string Name => "StubCanonical";

            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult<CanonicalTip>(null);

            public StubCanonicalSource(
                Dictionary<ulong, byte[]> byBlock = null,
                bool throwOnCall = false)
            {
                _byBlock = byBlock ?? new Dictionary<ulong, byte[]>();
                _throwOnCall = throwOnCall;
            }

            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
                ulong blockNumber, CancellationToken ct)
            {
                Queries.Add(blockNumber);
                if (_throwOnCall) throw new InvalidOperationException("stub canonical: simulated transport failure");
                if (!_byBlock.TryGetValue(blockNumber, out var root))
                    return Task.FromResult<(byte[], byte[])>((null, null));
                return Task.FromResult<(byte[], byte[])>((root, FillBytes((byte)(blockNumber + 0x80))));
            }
        }

        private sealed class AnchorEveryPolicy : IValidationPolicy
        {
            private readonly ulong _anchorEvery;
            public ValidationAction VerdictAction { get; set; } = ValidationAction.Continue;
            public List<(ulong Block, DivergenceOutcome Outcome)> ReceivedVerdicts { get; } = new();

            public AnchorEveryPolicy(ulong anchorEvery) { _anchorEvery = anchorEvery; }
            public bool ShouldAnchorAt(ulong blockNumber)
                => _anchorEvery > 0 && blockNumber > 0 && blockNumber % _anchorEvery == 0;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber)
            {
                ReceivedVerdicts.Add((blockNumber, verdict.Outcome));
                return VerdictAction;
            }
        }

        [Fact]
        public async Task RunAsync_PeriodicAnchor_StateRootAgrees_ContinuesForward_NoOnVerdict()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var bundles = Enumerable.Range(1, 4).Select(i => MakeBundle((ulong)i)).ToList();
            var source = new LocalReplayBlockSource(bundles);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());

            var canonical = new StubCanonicalSource(new Dictionary<ulong, byte[]>
            {
                { 2UL, FillBytes(2) },
                { 4UL, FillBytes(4) },
            });
            var policy = new AnchorEveryPolicy(anchorEvery: 2);
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, canonical,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 2), default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(4UL, result.LastExecutedBlock);
            Assert.Equal(4UL, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Equal(new[] { 2UL, 4UL }, canonical.Queries.ToArray());
            Assert.Empty(policy.ReceivedVerdicts);
        }

        [Fact]
        public async Task RunAsync_PeriodicAnchor_CanonicalUnavailable_ContinuesForward_NoFalseFail()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var bundles = Enumerable.Range(1, 4).Select(i => MakeBundle((ulong)i)).ToList();
            var source = new LocalReplayBlockSource(bundles);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());

            var canonical = new StubCanonicalSource(throwOnCall: true);
            var policy = new AnchorEveryPolicy(anchorEvery: 2);
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, canonical,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 2), default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(4UL, result.LastExecutedBlock);
            Assert.Equal(4UL, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Empty(policy.ReceivedVerdicts);
        }

        [Fact]
        public async Task RunAsync_PeriodicAnchor_Disabled_LegacyBehaviourUnchanged()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var bundles = Enumerable.Range(1, 4).Select(i => MakeBundle((ulong)i)).ToList();
            var source = new LocalReplayBlockSource(bundles);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());

            var canonical = new StubCanonicalSource(new Dictionary<ulong, byte[]>
            {
                { 2UL, FillBytes(0xCC) },
                { 4UL, FillBytes(0xCC) },
            });
            var policy = new AnchorEveryPolicy(anchorEvery: 0);
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, canonical,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0), default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(4UL, result.LastExecutedBlock);
            Assert.Equal(4UL, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Empty(canonical.Queries);
            Assert.Empty(policy.ReceivedVerdicts);
        }

        [Fact]
        public async Task RunAsync_PeriodicAnchor_CanonicalMismatch_PolicyFatal_ReturnsFatalVerdict()
        {
            var bundle = InMemoryChainStoreBundle.Open();
            var bundles = Enumerable.Range(1, 4).Select(i => MakeBundle((ulong)i)).ToList();
            var source = new LocalReplayBlockSource(bundles);
            var executor = new ScriptedExecutor(new Dictionary<ulong, bool>());

            var canonical = new StubCanonicalSource(new Dictionary<ulong, byte[]>
            {
                { 2UL, FillBytes(0xCC) },
            });
            var policy = new AnchorEveryPolicy(anchorEvery: 2) { VerdictAction = ValidationAction.Fatal };
            var follower = new FollowerService();

            var result = await follower.RunAsync(
                source, BundleFactoryFor(bundle), _ => executor, policy, canonical,
                new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 2), default);

            Assert.Equal(FollowerExitReason.FatalVerdict, result.ExitReason);
            Assert.Equal(2UL, result.LastExecutedBlock);
            Assert.Equal(1UL, result.RootMismatches);
            Assert.Contains("periodic anchor fatal", result.Detail);
            Assert.Single(policy.ReceivedVerdicts);
            Assert.Equal(2UL, policy.ReceivedVerdicts[0].Block);
        }
    }
}
