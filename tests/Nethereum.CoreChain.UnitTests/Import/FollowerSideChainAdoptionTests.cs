using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain;
using Nethereum.CoreChain.Validation;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class FollowerSideChainAdoptionTests
    {
        private sealed class CompetingBranchSource : IBlockSource
        {
            private readonly IList<BlockBundle> _first;
            private readonly DivergenceSignal _competing;
            private readonly IList<BlockBundle> _second;

            public CompetingBranchSource(IList<BlockBundle> first, DivergenceSignal competing, IList<BlockBundle> second)
            {
                _first = first;
                _competing = competing;
                _second = second;
            }

            public List<ulong> StreamStarts { get; } = new();

            public DivergenceSignal LastChainBreak => StreamStarts.Count == 1 ? _competing : null;

            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock, [EnumeratorCancellation] CancellationToken ct)
            {
                var index = StreamStarts.Count;
                StreamStarts.Add(fromBlock);
                var bundles = index == 0 ? _first : index == 1 ? _second : new List<BlockBundle>();
                foreach (var bundle in bundles)
                {
                    yield return bundle;
                    await Task.Yield();
                }
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class RecordingReconciler : IMempoolReorgReconciler
        {
            public List<(ulong Ancestor, ulong OrphanedHead, int Orphaned, int Winning)> Calls { get; } = new();
            public List<byte[]> OrphanedHashes { get; } = new();

            public Task ReconcileAsync(
                IChainStoreBundle bundle, ulong commonAncestor, ulong orphanedHead,
                IReadOnlyList<ISignedTransaction> winningBranchTransactions, CancellationToken ct) =>
                Task.CompletedTask;

            public Task ReconcileAsync(
                IChainStoreBundle bundle, ulong commonAncestor, ulong orphanedHead,
                IReadOnlyList<ISignedTransaction> orphanedBranchTransactions,
                IReadOnlyList<ISignedTransaction> winningBranchTransactions, CancellationToken ct)
            {
                Calls.Add((commonAncestor, orphanedHead, orphanedBranchTransactions.Count, winningBranchTransactions.Count));
                OrphanedHashes.AddRange(orphanedBranchTransactions.Select(tx => tx.Hash));
                return Task.CompletedTask;
            }
        }

        private sealed class AcceptEverythingPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;

            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private static DevChainConfig PreShanghai() => new DevChainConfig { Hardfork = "paris", AutoMine = false };

        private static BlockBundle BundleOf(ProducedBlock block) =>
            new BlockBundle(block.Header, block.Transactions, new List<BlockHeader>(), block.Withdrawals, block.Hash);

        [Fact]
        public async Task Given_ACompetingBlockWhoseBranchIsAdopted_When_TheFollowerHandsItToTheImporter_Then_TheChainIsReorganisedAndTheMempoolReceivesTheOrphanedTransactions()
        {
            using var producerA = await SideChainHarness.CreateAsync(withSideChain: false, config: PreShanghai());
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false, config: PreShanghai());
            using var node = await SideChainHarness.CreateAsync(adoptByDifficulty: true, config: PreShanghai());
            var transfer = BlockPipelineHarness.Transfer(0);
            var a1 = await producerA.ProduceAsync(
                SideChainHarness.RecipientA, transactions: new List<ISignedTransaction> { transfer }, withWithdrawals: false);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2, withWithdrawals: false);
            var competing = new DivergenceSignal(
                1, b[0].Header.ParentHash, a1.Header.ParentHash, 1, "test",
                b[0].Header, b[0].Hash, null, b[0].Transactions.ToList());
            var source = new CompetingBranchSource(
                new[] { BundleOf(a1) }, competing, new[] { BundleOf(b[1]) });
            var reconciler = new RecordingReconciler();

            var result = await new FollowerService(node.SideChain.ForkChoice, reconciler).RunAsync(
                source,
                () => node.Bundle,
                _ => node.Importer,
                new AcceptEverythingPolicy(),
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0),
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(b[1].Hash, await node.HeadHashAsync());
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(BlockPipelineHarness.RecipientAddress));
            Assert.Equal(2UL, node.Bundle.Metadata.GetLastBlock());
            Assert.Equal(new ulong[] { 1, 2 }, source.StreamStarts.ToArray());
            var call = Assert.Single(reconciler.Calls);
            Assert.Equal(0UL, call.Ancestor);
            Assert.Equal(1UL, call.OrphanedHead);
            Assert.Equal(1, call.Orphaned);
            Assert.Equal(transfer.Hash, Assert.Single(reconciler.OrphanedHashes));
        }
    }
}
