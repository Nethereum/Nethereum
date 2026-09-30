using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockImporterWithdrawalPersistenceTests
    {
        private static List<Withdrawal> OneWithdrawal() => new()
        {
            new Withdrawal { Index = 11, ValidatorIndex = 22, Address = new byte[20], AmountInGwei = 33 }
        };

        private static async Task<BlockHeader> ProduceBlockAsync(HardforkName fork, List<Withdrawal> withdrawals)
        {
            var producerHarness = await BlockPipelineHarness.CreateAsync(fork);
            var result = await producerHarness.Producer().ProduceBlockAsync(
                new List<ISignedTransaction>(),
                new BlockProductionOptions
                {
                    Timestamp = 1_700_000_000,
                    Coinbase = BlockPipelineHarness.SenderAddress,
                    BaseFee = 0,
                    BlockGasLimit = 30_000_000,
                    Difficulty = 1,
                    ChainId = BlockPipelineHarness.ChainId,
                    Withdrawals = withdrawals
                });
            return result.Header;
        }

        [Fact]
        public async Task Given_a_post_Shanghai_block_with_withdrawals_When_ImportAsync_persists_Then_the_withdrawal_store_holds_the_exact_list()
        {
            var withdrawals = OneWithdrawal();
            var header = await ProduceBlockAsync(HardforkName.Shanghai, withdrawals);

            var importHarness = await BlockPipelineHarness.CreateAsync(HardforkName.Shanghai);
            var withdrawalStore = new InMemoryWithdrawalStore();
            var importer = new BlockImporter(
                importHarness.Engine, importHarness.BlockStore, importHarness.StateStore,
                withdrawalStore: withdrawalStore);

            var result = await importer.ImportAsync(
                header, new List<ISignedTransaction>(), uncles: null, withdrawals, CancellationToken.None);
            Assert.True(result.RootMatches, result.DescribeRejection());

            var stored = await withdrawalStore.GetByBlockHashAsync(result.BlockHash);
            Assert.NotNull(stored);
            var withdrawal = Assert.Single(stored);
            Assert.Equal(11UL, withdrawal.Index);
            Assert.Equal(22UL, withdrawal.ValidatorIndex);
            Assert.Equal(33UL, withdrawal.AmountInGwei);
        }

        [Fact]
        public async Task Given_a_post_Shanghai_block_with_zero_withdrawals_When_ImportAsync_persists_Then_the_withdrawal_store_holds_an_empty_not_null_list()
        {
            var withdrawals = new List<Withdrawal>();
            var header = await ProduceBlockAsync(HardforkName.Shanghai, withdrawals);

            var importHarness = await BlockPipelineHarness.CreateAsync(HardforkName.Shanghai);
            var withdrawalStore = new InMemoryWithdrawalStore();
            var importer = new BlockImporter(
                importHarness.Engine, importHarness.BlockStore, importHarness.StateStore,
                withdrawalStore: withdrawalStore);

            var result = await importer.ImportAsync(
                header, new List<ISignedTransaction>(), uncles: null, withdrawals, CancellationToken.None);
            Assert.True(result.RootMatches, result.DescribeRejection());

            var stored = await withdrawalStore.GetByBlockHashAsync(result.BlockHash);
            Assert.NotNull(stored);
            Assert.Empty(stored);
        }

        [Fact]
        public async Task Given_a_pre_Shanghai_block_When_ImportAsync_persists_Then_the_withdrawal_store_is_never_called()
        {
            var header = await ProduceBlockAsync(HardforkName.Paris, withdrawals: null);
            Assert.Null(header.WithdrawalsRoot);

            var importHarness = await BlockPipelineHarness.CreateAsync(HardforkName.Paris);
            var withdrawalStore = new InMemoryWithdrawalStore();
            var importer = new BlockImporter(
                importHarness.Engine, importHarness.BlockStore, importHarness.StateStore,
                withdrawalStore: withdrawalStore);

            var result = await importer.ImportAsync(
                header, new List<ISignedTransaction>(), uncles: null, withdrawals: null, CancellationToken.None);
            Assert.True(result.RootMatches, result.DescribeRejection());

            var stored = await withdrawalStore.GetByBlockHashAsync(result.BlockHash);
            Assert.Null(stored);
        }

        [Fact]
        public async Task Given_no_withdrawal_store_wired_When_ImportAsync_runs_Then_import_still_succeeds()
        {
            var withdrawals = OneWithdrawal();
            var header = await ProduceBlockAsync(HardforkName.Shanghai, withdrawals);

            var importHarness = await BlockPipelineHarness.CreateAsync(HardforkName.Shanghai);
            var importer = importHarness.Importer();

            var result = await importer.ImportAsync(
                header, new List<ISignedTransaction>(), uncles: null, withdrawals, CancellationToken.None);
            Assert.True(result.RootMatches, result.DescribeRejection());
        }
    }
}
