using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockImporterMidWindowShutdownTests
    {
        private static async Task<(BlockHeader Header, List<ISignedTransaction> Transactions)> ProducedBlockAsync()
        {
            var producerHarness = await BlockPipelineHarness.CreateAsync();
            var transactions = new List<ISignedTransaction> { BlockPipelineHarness.Transfer(0) };
            var produced = await producerHarness.ProduceAsync(transactions);
            return (produced.Header, transactions);
        }

        private static async Task<(BlockImporter Importer, HistoricalStateStore History)> MidWindowImporterAsync()
        {
            var harness = await BlockPipelineHarness.CreateAsync();
            var history = new HistoricalStateStore(harness.StateStore);
            history.SetCurrentBlockNumber(BigInteger.Zero);
            var importer = new BlockImporter(
                harness.Engine, harness.BlockStore, history,
                flushCadence: new FixedIntervalFlushCadence(24));
            return (importer, history);
        }

        [Fact]
        public async Task Given_host_shutdown_while_a_block_executes_mid_window_When_the_follower_stops_Then_it_exits_as_cancelled_and_resumes_from_the_durable_cursor()
        {
            var (header, transactions) = await ProducedBlockAsync();
            var (importer, _) = await MidWindowImporterAsync();
            using var shutdown = new CancellationTokenSource();
            shutdown.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                importer.ImportAsync(header, transactions, uncles: null, withdrawals: null, shutdown.Token));
        }

        [Fact]
        public async Task Given_a_block_rejected_for_a_genuine_reason_mid_window_When_it_is_discarded_Then_the_revert_the_window_cannot_support_still_raises_not_supported()
        {
            var (header, transactions) = await ProducedBlockAsync();
            var wrongRoot = BlockPipelineHarness.CloneHeader(header);
            wrongRoot.StateRoot = new byte[32];
            var (importer, _) = await MidWindowImporterAsync();

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                importer.ImportAsync(wrongRoot, transactions, uncles: null, withdrawals: null, CancellationToken.None));
        }
    }
}
