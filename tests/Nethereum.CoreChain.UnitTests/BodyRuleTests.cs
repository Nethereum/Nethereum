using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BodyRuleTests
    {
        private static void AssertRefusedOnlyFor(BlockValidityCheck check, BlockImporterResult result)
        {
            Assert.Equal(new[] { check }, result.FailedValidityChecks);
            Assert.Null(result.ComputedStateRoot);
            Assert.Null(result.BlockHash);
        }

        private static BlockHeader AnUncle(HeaderRuleHarness harness) => harness.ValidChild();

        [Fact]
        public async Task Given_APostMergeBlock_When_TheBodyCarriesAnUncle_Then_TheBlockIsRefusedForUncles()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);

            var result = await harness.ImportAsync(harness.ValidChild(), new List<BlockHeader> { AnUncle(harness) });

            AssertRefusedOnlyFor(BlockValidityCheck.Uncles, result);
        }

        [Fact]
        public async Task Given_APreMergeBlock_When_TheHeaderUnclesHashIsNotTheHashOfTheBodyUncles_Then_TheBlockIsRefusedForUnclesHash()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Berlin);
            var header = harness.ValidChild();
            header.UnclesHash = new byte[32];

            AssertRefusedOnlyFor(BlockValidityCheck.UnclesHash, await harness.ImportAsync(header));
        }

        [Fact]
        public async Task Given_APreMergeBlock_When_TheHeaderUnclesHashIsTheEmptyListHashOfAnEmptyBody_Then_NoUnclesHashRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Berlin);

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.UnclesHash, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_ABodyWhoseTransactionsDoNotHashToTheHeaderRoot_When_Imported_Then_TheBlockIsRefusedForTransactionsRoot()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Prague);
            var header = harness.ValidChild();
            header.TransactionsHash = new byte[32];

            var result = await harness.ImportAsync(header);

            Assert.Contains(BlockValidityCheck.TransactionsRoot, result.FailedValidityChecks);
            Assert.Null(result.BlockHash);
        }

        [Fact]
        public async Task Given_AShanghaiBlock_When_TheBodyCarriesAnEmptyWithdrawalsList_Then_NoWithdrawalsRootRefusal()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Shanghai);

            var result = await harness.ImportAsync(harness.ValidChild());

            Assert.DoesNotContain(BlockValidityCheck.WithdrawalsRoot, result.FailedValidityChecks);
        }

        [Fact]
        public async Task Given_AParisBlock_When_TheBodyCarriesWithdrawals_Then_TheBlockIsRefusedForWithdrawalsRoot()
        {
            var harness = await HeaderRuleHarness.CreateAsync(HardforkName.Paris);
            var withdrawals = new List<Withdrawal>
            {
                new Withdrawal { Index = 1, ValidatorIndex = 2, Address = new byte[20], AmountInGwei = 3 }
            };

            var result = await harness.ImportWithBodyAsync(harness.ValidChild(), uncles: null, withdrawals);

            AssertRefusedOnlyFor(BlockValidityCheck.WithdrawalsRoot, result);
        }
    }
}
