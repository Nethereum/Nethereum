using System.Threading.Tasks;
using Nethereum.EVM;
using Nethereum.EVM.Execution;
using Xunit;
using static Nethereum.CoreChain.UnitTests.SystemCallBlockHarness;

namespace Nethereum.CoreChain.UnitTests
{
    public class WitnessModellingOneTransactionTests
    {
        private const long AfterPrague = 1_700_000_000;

        private static Task<System.Exception> ExecuteWithoutTheWithdrawalPredeployAsync(bool asASingleTransaction) =>
            ExecuteGuestVerdictAsync(
                HardforkName.Amsterdam, AfterPrague,
                WitnessCarryingEveryActivatedPredeploy(
                    HardforkName.Amsterdam, SystemCallContracts.WithdrawalRequests, new byte[0]),
                skipsRequestSystemCalls: asASingleTransaction);

        [Fact]
        [Trait("Category", "EIP7002")]
        public async Task Given_AWitnessModellingABlock_When_TheWithdrawalPredeployHasNoCode_Then_TheBlockIsRefused()
        {
            var refusal = await ExecuteWithoutTheWithdrawalPredeployAsync(asASingleTransaction: false);

            Assert.IsType<SystemCallPredeployMissingException>(refusal);
        }

        [Fact]
        [Trait("Category", "EIP7002")]
        public async Task Given_AWitnessModellingOneTransaction_When_TheWithdrawalPredeployHasNoCode_Then_ThereIsNoRequestQueueToRefuse()
        {
            var refusal = await ExecuteWithoutTheWithdrawalPredeployAsync(asASingleTransaction: true);

            Assert.Null(refusal);
        }
    }
}
