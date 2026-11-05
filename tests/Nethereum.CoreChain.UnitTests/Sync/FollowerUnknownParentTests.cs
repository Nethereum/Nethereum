using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class FollowerUnknownParentTests
    {
        private sealed class ScriptedStreams : IBlockSource
        {
            private readonly List<IList<BlockBundle>> _streams;

            public ScriptedStreams(params IList<BlockBundle>[] streams) => _streams = new List<IList<BlockBundle>>(streams);

            public List<ulong> StreamStarts { get; } = new();
            public List<(ulong Block, BadBundleReason Reason)> BadBundleReports { get; } = new();
            public DivergenceSignal LastChainBreak => null;

            public async IAsyncEnumerable<BlockBundle> StreamAsync(
                ulong fromBlock, [EnumeratorCancellation] CancellationToken ct)
            {
                var index = StreamStarts.Count;
                StreamStarts.Add(fromBlock);
                if (index >= _streams.Count) yield break;
                foreach (var bundle in _streams[index])
                {
                    yield return bundle;
                    await Task.Yield();
                }
            }

            public Task<BlockSourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(BlockSourceHealth.Healthy);

            public Task ReportBadBundleAsync(ulong blockNumber, BadBundleReason reason, CancellationToken ct)
            {
                BadBundleReports.Add((blockNumber, reason));
                return Task.CompletedTask;
            }
        }

        private static BlockBundle BundleOf(BlockProductionResult produced) =>
            new BlockBundle(
                produced.Header,
                new List<ISignedTransaction>(),
                new List<BlockHeader>(),
                new List<Withdrawal>(),
                produced.BlockHash);

        [Fact]
        public async Task Given_ABundleWhoseParentIsUnknown_When_Followed_Then_ItIsAGapNotAnInvalidBlock()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var first = await author.ProduceAsync(new List<ISignedTransaction>());
            var second = await author.Producer().ProduceBlockAsync(new List<ISignedTransaction>(), new BlockProductionOptions
            {
                Timestamp = 1_700_000_012,
                Coinbase = BlockPipelineHarness.SenderAddress,
                BaseFee = 0,
                BlockGasLimit = 30_000_000,
                Difficulty = 0,
                ChainId = BlockPipelineHarness.ChainId
            });

            var followerStores = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(followerStores.State, HardforkName.Prague);
            await followerStores.State.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });
            var genesisHash = await ShapedHeaders.SaveGenesisAsync(followerStores.Blocks, HardforkName.Prague, 30_000_000);
            followerStores.Metadata.Commit(0, genesisHash);

            var config = new ChainConfig { ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var executor = FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                followerStores, new FixedChainActivations(HardforkName.Prague),
                _ => config, _ => config.GetHardforkConfig(), NoRewardPolicy.Instance);

            var source = new ScriptedStreams(
                new[] { BundleOf(second) },
                new[] { BundleOf(first), BundleOf(second) });

            var result = await new FollowerService().RunAsync(
                source,
                () => followerStores,
                _ => executor,
                new AcceptEverythingPolicy(),
                canonical: null,
                options: new FollowerOptions(StartBlock: 1, CheckpointEvery: 0, AnchorEvery: 0)
                {
                    ChainWriteGate = new AsyncGate()
                },
                ct: default);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(2UL, result.LastExecutedBlock);
            Assert.Equal(2UL, result.BlocksExecuted);
            Assert.Equal(0UL, result.RootMismatches);
            Assert.Empty(source.BadBundleReports);
            Assert.Equal(new ulong[] { 1, 1 }, source.StreamStarts.ToArray());
        }

        private sealed class AcceptEverythingPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;

            public ValidationAction OnVerdict(
                DivergenceVerdict verdict, ulong blockNumber) =>
                ValidationAction.Continue;
        }
    }
}
