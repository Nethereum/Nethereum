using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Sync;
using Nethereum.CoreChain.Validation;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Sync
{
    public class FollowerWriteGateTests
    {
        private sealed class FixedTip : ICanonicalStateRootSource
        {
            private readonly CanonicalTip _tip;
            public FixedTip(CanonicalTip tip) { _tip = tip; }
            public string Name => "fixed";
            public Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(ulong blockNumber, CancellationToken ct)
                => Task.FromResult<(byte[], byte[])>((null, null));
            public Task<CanonicalTip> GetLatestAsync(CancellationToken ct) => Task.FromResult(_tip);
        }

        private sealed class RecordingExecutor : IBlockExecutor
        {
            private readonly IBlockExecutor _inner;
            public RecordingExecutor(IBlockExecutor inner) { _inner = inner; }
            public List<ulong> Executed { get; } = new();

            public async Task<BlockImporterResult> ProcessBlockAsync(
                BlockHeader header, IList<ISignedTransaction> transactions, IList<BlockHeader> uncles,
                IList<Withdrawal> withdrawals, CancellationToken ct)
            {
                var result = await _inner.ProcessBlockAsync(header, transactions, uncles, withdrawals, ct);
                lock (Executed) Executed.Add((ulong)header.BlockNumber);
                return result;
            }

            public int Count { get { lock (Executed) return Executed.Count; } }
        }

        private sealed class AcceptEverythingPolicy : IValidationPolicy
        {
            public bool ShouldAnchorAt(ulong blockNumber) => false;
            public ValidationAction OnVerdict(DivergenceVerdict verdict, ulong blockNumber) => ValidationAction.Continue;
        }

        private sealed class Scenario
        {
            public IChainStoreBundle Bundle;
            public RecordingExecutor Executor;
            public CanonicalTip Tip;
        }

        private static async Task<Scenario> ArrangeAsync()
        {
            var author = await BlockPipelineHarness.CreateAsync();
            var blocks = new List<BlockProductionResult>();
            for (var n = 1; n <= 2; n++)
            {
                blocks.Add(await author.Producer().ProduceBlockAsync(
                    new List<ISignedTransaction> { BlockPipelineHarness.Transfer(n - 1) },
                    new BlockProductionOptions
                    {
                        Timestamp = 1_700_000_000 + 12 * (n - 1),
                        Coinbase = BlockPipelineHarness.SenderAddress,
                        BaseFee = 0,
                        BlockGasLimit = 30_000_000,
                        Difficulty = 0,
                        ChainId = BlockPipelineHarness.ChainId
                    }));
            }

            var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, HardforkName.Prague);
            await bundle.State.SaveAccountAsync(BlockPipelineHarness.SenderAddress, new Account
            {
                Balance = 1_000_000_000_000_000,
                Nonce = 0
            });
            var genesisHash = await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, HardforkName.Prague, 30_000_000);
            bundle.Metadata.Commit(0, genesisHash);
            foreach (var block in blocks)
            {
                await bundle.Blocks.SaveAsync(block.Header, block.BlockHash);
                await bundle.Uncles.SaveAsync(block.BlockHash, new List<BlockHeader>());
                await bundle.Transactions.SaveManyAsync(block.BlockHash, block.Header.BlockNumber, block.IncludedTransactions);
            }

            var config = new ChainConfig { ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0 };
            var real = FollowerExecutorStackFactory.BuildFollowerExecutorStack(
                bundle, new FixedChainActivations(HardforkName.Prague),
                _ => config, _ => config.GetHardforkConfig(), NoRewardPolicy.Instance);

            return new Scenario
            {
                Bundle = bundle,
                Executor = new RecordingExecutor(real),
                Tip = new CanonicalTip { BlockNumber = 2, BlockHash = blocks[1].BlockHash }
            };
        }

        private static Task<FollowerRunResult> RunAsync(Scenario scenario, AsyncGate gate, CancellationToken ct)
        {
            BackwardWalkerDelegate neverWalks = (f, h, t, b, c, n) =>
                throw new InvalidOperationException("the external-header-follow path must not walk");
            return new FollowerService(neverWalks, null, null).RunAsync(
                new LocalReplayBlockSource(new List<BlockBundle>()),
                () => scenario.Bundle,
                _ => scenario.Executor,
                new AcceptEverythingPolicy(),
                new FixedTip(scenario.Tip),
                new FollowerOptions(StartBlock: 0, CheckpointEvery: 0, AnchorEvery: 0, EndBlock: 2,
                    TipPollInterval: TimeSpan.FromMilliseconds(10))
                { ExternalHeaderFollow = true, ChainWriteGate = gate },
                ct);
        }

        [Fact]
        public async Task Given_TheWriteGateIsHeldByAnotherWriter_When_TheTipFollowerHasBlocksToExecute_Then_NoBlockExecutesUntilTheGateIsReleased()
        {
            var scenario = await ArrangeAsync();
            var gate = new AsyncGate();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await gate.WaitAsync();

            var run = RunAsync(scenario, gate, cts.Token);
            await Task.Delay(500);

            Assert.Equal(0, scenario.Executor.Count);
            Assert.False(run.IsCompleted);

            gate.Release();
            var result = await run;

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(new ulong[] { 1, 2 }, scenario.Executor.Executed.ToArray());
        }

        [Fact]
        public async Task Given_NoWriteGate_When_TheTipFollowerHasBlocksToExecute_Then_TheBlocksExecuteWithoutWaiting()
        {
            var scenario = await ArrangeAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var result = await RunAsync(scenario, gate: null, cts.Token);

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(new ulong[] { 1, 2 }, scenario.Executor.Executed.ToArray());
        }

        [Fact]
        public async Task Given_ATipFollowerWithAWriteGate_When_ItRuns_Then_ItPublishesItsCurrentExecutorThroughTheGate()
        {
            var scenario = await ArrangeAsync();
            var gate = new AsyncGate();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await RunAsync(scenario, gate, cts.Token);

            Assert.Same(scenario.Executor, gate.Executor);
        }

        [Fact]
        public async Task Given_AnotherWriterAlreadyExecutedABlock_When_TheTipFollowerReachesIt_Then_ItSkipsTheBlockInsteadOfExecutingItTwice()
        {
            var scenario = await ArrangeAsync();
            var gate = new AsyncGate();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await gate.WaitAsync();

            var run = RunAsync(scenario, gate, cts.Token);
            await Task.Delay(300);
            var first = await scenario.Bundle.Blocks.GetByNumberAsync(1);
            var firstHash = await scenario.Bundle.Blocks.GetHashByNumberAsync(1);
            var otherWriter = await scenario.Executor.ProcessBlockAsync(
                first, await scenario.Bundle.Transactions.GetByBlockHashAsync(firstHash), new List<BlockHeader>(), null, cts.Token);
            Assert.True(otherWriter.RootMatches);
            gate.SetHead(1, firstHash);
            gate.Release();
            var result = await run;

            Assert.Equal(FollowerExitReason.SourceCompleted, result.ExitReason);
            Assert.Equal(new ulong[] { 1, 2 }, scenario.Executor.Executed.ToArray());
        }
    }
}
