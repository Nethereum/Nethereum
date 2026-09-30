using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    /// <summary>
    /// Descriptive tests for <see cref="GaslessViewModel"/> - paymaster-sponsored gas through the
    /// example's on-ramp, against a real deployed <c>VerifyingPaymasterService</c> and the in-process
    /// bundler with ERC-4337 validation ON (never a mock paymaster or a mock bundler). Read these as
    /// usage documentation for "the account never pays its own gas".
    /// </summary>
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class GaslessViewModelTests
    {
        private readonly ExampleFixture _fixture;

        public GaslessViewModelTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private SessionState NewSession() => _fixture.NewReadySession();

        [Fact]
        [Trait("UseCase", "Gasless")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#paymaster-sponsorship")]
        public async Task Given_a_funded_sponsoring_paymaster_When_a_zero_balance_account_sends_Then_the_paymaster_pays_and_the_account_balance_stays_zero()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            var accountAddress = session.Account!.Address;
            var balanceBefore = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceBefore.Value);

            session.Paymaster = await _fixture.DeploySponsoringPaymasterAsync(depositEth: 5m);
            var gaslessViewModel = new GaslessViewModel(session);

            await gaslessViewModel.SendGaslessCommand.ExecuteAsync(null);

            Assert.False(gaslessViewModel.IsBusy);
            Assert.True(string.IsNullOrEmpty(gaslessViewModel.ErrorMessage), gaslessViewModel.ErrorMessage);
            Assert.NotNull(gaslessViewModel.LastReceipt);
            Assert.True(gaslessViewModel.LastReceipt!.UserOpSuccess, gaslessViewModel.LastReceipt.FailureDiagnostic);

            Assert.True(gaslessViewModel.SenderBalanceUnchanged);
            Assert.Equal(BigInteger.Zero, gaslessViewModel.SenderBalanceAfter);
            var balanceAfter = await _fixture.TestCounter.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, balanceAfter.Value);

            var onChainCount = await _fixture.TestCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, onChainCount);
        }

        [Fact]
        [Trait("UseCase", "Gasless")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#paymaster-insufficient-deposit")]
        public async Task Given_a_paymaster_with_insufficient_deposit_When_sponsoring_Then_the_operation_is_rejected()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);

            session.Paymaster = await _fixture.DeploySponsoringPaymasterAsync(depositEth: 0m);
            var gaslessViewModel = new GaslessViewModel(session);

            await gaslessViewModel.SendGaslessCommand.ExecuteAsync(null);

            Assert.False(gaslessViewModel.IsBusy);
            Assert.Null(gaslessViewModel.LastReceipt);
            Assert.False(string.IsNullOrEmpty(gaslessViewModel.ErrorMessage));
            Assert.Contains("AA31", gaslessViewModel.ErrorMessage);
            Assert.Contains("deposit", gaslessViewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

            var onChainCount = await _fixture.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(BigInteger.Zero, onChainCount);
        }
    }
}
