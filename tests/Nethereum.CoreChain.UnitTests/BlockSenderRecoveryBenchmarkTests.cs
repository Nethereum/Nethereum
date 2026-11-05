using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.UnitTests
{
    [Trait("Category", "Benchmark")]
    [Trait("Category", "LargeFixture")]
    public class BlockSenderRecoveryBenchmarkTests
    {
        private const HardforkName Fork = HardforkName.Prague;
        private const int Transfers = 1000;
        private const int Repetitions = 5;
        private const long Funding = 1_000_000_000_000_000_000;

        private static readonly ChainConfig Config = new ChainConfig
        {
            ChainId = BlockPipelineHarness.ChainId, BlockGasLimit = 30_000_000, BaseFee = 0
        };

        private readonly ITestOutputHelper _output;

        public BlockSenderRecoveryBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static async Task<(BlockHeader Header, List<ISignedTransaction> Transactions)> ProduceTransfersAsync()
        {
            var producer = await BlockPipelineHarness.CreateAsync(Fork);
            await producer.StateStore.SaveAccountAsync(
                BlockPipelineHarness.SenderAddress, new Account { Balance = Funding, Nonce = 0 });
            var transactions = Enumerable.Range(0, Transfers).Select(nonce => BlockPipelineHarness.Transfer(nonce)).ToList();
            var produced = await producer.ProduceAsync(transactions);
            return (produced.Header, transactions);
        }

        private static async Task<double> ExecuteOnFollowerStackAsync(
            BlockHeader header, List<ISignedTransaction> transactions, int prefetchWorkers)
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(bundle.State, Fork);
            await bundle.State.SaveAccountAsync(
                BlockPipelineHarness.SenderAddress, new Account { Balance = Funding, Nonce = 0 });
            await ShapedHeaders.SaveGenesisAsync(bundle.Blocks, Fork, 30_000_000);
            using var stack = ExecutionStackFactory.Build(
                bundle, new FixedChainActivations(Fork), _ => Config, _ => Config.GetHardforkConfig(),
                new ExecutionStackOptions { RewardPolicy = NoRewardPolicy.Instance, PrefetchWorkers = prefetchWorkers });
            var session = stack.Prefetcher?.Begin(header, transactions);

            var watch = Stopwatch.StartNew();
            var result = await stack.Executor.ExecuteAsync(
                header,
                transactions.Select(t => new TxEntry(t)).ToList(),
                null,
                null,
                new BlockExecutionOptions { Role = BlockExecutionRole.Validating, Prefetch = session });
            var elapsed = watch.Elapsed.TotalMilliseconds;

            session?.Stop();
            Assert.Equal(header.StateRoot, result.PostStateRoot);
            return elapsed;
        }

        private async Task ReportAsync(string composition, int prefetchWorkers)
        {
            var (header, transactions) = await ProduceTransfersAsync();
            var timings = new List<double>();
            for (var i = 0; i < Repetitions; i++)
                timings.Add(await ExecuteOnFollowerStackAsync(header, transactions, prefetchWorkers));
            timings.Sort();
            _output.WriteLine(
                $"{composition}: {Transfers} transfers, execute min {timings[0]:F1} ms, median {timings[Repetitions / 2]:F1} ms");
        }

        [Fact]
        public Task Given_ABlockOfEtherTransfers_When_ExecutedWithoutAPrefetcher_Then_TheExecuteTimeIsReported() =>
            ReportAsync("harness composition (no prefetcher)", prefetchWorkers: 0);

        [Fact]
        public Task Given_ABlockOfEtherTransfers_When_ExecutedWithThePrefetcher_Then_TheExecuteTimeIsReported() =>
            ReportAsync("follower composition (prefetcher 4 workers)", prefetchWorkers: 4);
    }
}
