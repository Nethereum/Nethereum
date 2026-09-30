using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(ExampleCollection.COLLECTION_NAME)]
    public class ViewModelTests
    {
        private readonly ExampleFixture _fixture;

        public ViewModelTests(ExampleFixture fixture)
        {
            _fixture = fixture;
        }

        private SessionState NewSession() => _fixture.NewReadySession();

        [Fact]
        [Trait("UseCase", "Setup")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#create-account")]
        public async Task Given_a_configured_client_When_the_user_creates_an_account_Then_a_counterfactual_smart_account_address_is_shown()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);

            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);

            Assert.Null(setupViewModel.ErrorMessage);
            Assert.False(string.IsNullOrEmpty(setupViewModel.SmartAccountAddress));
            Assert.False(string.IsNullOrEmpty(setupViewModel.OwnerAddress));
            Assert.False(setupViewModel.IsDeployed);
            Assert.NotNull(session.Account);
            Assert.Equal(setupViewModel.SmartAccountAddress, session.Account!.Address);
            Assert.Equal("ECDSA owner (secp256k1)", session.AccountDescription);
        }

        [Fact]
        [Trait("UseCase", "Setup")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#fund-account")]
        public async Task Given_a_freshly_created_account_When_the_user_funds_it_Then_the_faucet_reports_a_positive_balance()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);

            var balanceBefore = await _fixture.Faucet.GetBalanceAsync(session.Account!.Address);
            Assert.Equal(BigInteger.Zero, balanceBefore);

            await setupViewModel.FundAccountCommand.ExecuteAsync(null);

            Assert.Null(setupViewModel.ErrorMessage);
            var balanceAfter = await _fixture.Faucet.GetBalanceAsync(session.Account.Address);
            Assert.True(balanceAfter > BigInteger.Zero);
        }

        [Fact]
        [Trait("UseCase", "Send")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#send-first-operation")]
        public async Task Given_a_new_smart_account_When_the_user_sends_the_first_operation_Then_the_account_deploys_and_the_call_succeeds()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            await _fixture.FundAsync(session.Account!.Address);

            var interactionViewModel = new InteractionViewModel(session);
            await interactionViewModel.SendCountCommand.ExecuteAsync(null);

            Assert.Null(interactionViewModel.ErrorMessage);
            Assert.NotNull(interactionViewModel.LastReceipt);
            Assert.True(interactionViewModel.LastReceipt!.UserOpSuccess, interactionViewModel.LastReceipt.RevertReason);
            Assert.Equal(BigInteger.One, interactionViewModel.Count);

            var codeAfterDeploy = await _fixture.Bootstrap.Node.GetCodeAsync(session.Account.Address);
            Assert.NotNull(codeAfterDeploy);
            Assert.True(codeAfterDeploy.Length > 0);
        }

        [Fact]
        [Trait("UseCase", "Send")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#send-failure")]
        public async Task Given_an_operation_that_reverts_When_the_user_sends_it_Then_a_readable_failure_is_surfaced_on_the_view_model()
        {
            var session = NewSession();
            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            await _fixture.FundAsync(session.Account!.Address);

            var interactionViewModel = new InteractionViewModel(session);
            await interactionViewModel.SendFailingCountCommand.ExecuteAsync(null);

            Assert.False(interactionViewModel.IsBusy);
            Assert.Null(interactionViewModel.LastReceipt);
            Assert.False(string.IsNullOrEmpty(interactionViewModel.ErrorMessage));
            Assert.Contains("count failed", interactionViewModel.ErrorMessage);
        }

        [Fact]
        [Trait("UseCase", "Harness")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#busy-and-error-harness")]
        public async Task Given_a_busy_command_When_it_runs_Then_IsBusy_toggles_and_errors_are_captured()
        {
            var session = NewSession();
            var interactionViewModel = new InteractionViewModel(session);
            var isBusyTransitions = new List<bool>();
            interactionViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(InteractionViewModel.IsBusy))
                    isBusyTransitions.Add(interactionViewModel.IsBusy);
            };

            await interactionViewModel.SendCountCommand.ExecuteAsync(null);

            Assert.Equal(new[] { true, false }, isBusyTransitions);
            Assert.False(interactionViewModel.IsBusy);
            Assert.False(string.IsNullOrEmpty(interactionViewModel.ErrorMessage));
            Assert.Contains("No active account", interactionViewModel.ErrorMessage);
        }
    }
}
