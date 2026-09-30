using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(PasskeyHostFixture.COLLECTION_NAME)]
    public class WorkflowsViewModelTests
    {
        private readonly PasskeyHostFixture _fixture;

        public WorkflowsViewModelTests(PasskeyHostFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<SessionState> NewFundedEcdsaAccountAsync()
        {
            var setupVm = _fixture.Services.GetRequiredService<SetupViewModel>();
            var session = _fixture.Services.GetRequiredService<SessionState>();

            await setupVm.CreateAccountCommand.ExecuteAsync(null);
            await _fixture.Infra.FundAsync(session.Account!.Address);
            return session;
        }

        [Fact]
        [Trait("UseCase", "Workflows")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#workflows-install")]
        public async Task Given_a_funded_ecdsa_account_and_an_agent_When_installing_the_executor_Then_isModuleInstalled_becomes_true()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<WorkflowsViewModel>();

            await vm.CreateAgentCommand.ExecuteAsync(null);
            Assert.False(string.IsNullOrEmpty(vm.AgentAddress), vm.ErrorMessage);

            await vm.InstallExecutorCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsExecutorInstalled);

            var code = await _fixture.Infra.Bootstrap.Node.GetCodeAsync(session.Account!.Address);
            Assert.NotNull(code);
            Assert.True(code.Length > 0);
        }

        [Fact]
        [Trait("UseCase", "Workflows")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#workflows-delegate")]
        public async Task Given_an_installed_executor_When_the_agent_runs_the_delegated_action_Then_a_plain_tx_lands_and_the_counter_increments()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<WorkflowsViewModel>();
            await vm.CreateAgentCommand.ExecuteAsync(null);
            await vm.InstallExecutorCommand.ExecuteAsync(null);
            Assert.True(vm.IsExecutorInstalled, vm.ErrorMessage);

            await vm.RunDelegatedActionCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.False(string.IsNullOrEmpty(vm.LastDelegatedTxHash));
            Assert.Equal(BigInteger.One, vm.Count);

            var onChainCount = await _fixture.Infra.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(BigInteger.One, onChainCount);
        }

        [Fact]
        [Trait("UseCase", "Workflows")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#workflows-unauthorized")]
        public async Task Given_an_installed_executor_When_an_unauthorized_EOA_tries_the_same_action_Then_it_is_rejected()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<WorkflowsViewModel>();
            await vm.CreateAgentCommand.ExecuteAsync(null);
            await vm.InstallExecutorCommand.ExecuteAsync(null);
            Assert.True(vm.IsExecutorInstalled, vm.ErrorMessage);

            await vm.TryUnauthorizedActionCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.UnauthorizedRejected == true, vm.StatusMessage);
            Assert.Contains("UnauthorizedAccess", vm.StatusMessage);

            var onChainCount = await _fixture.Infra.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(BigInteger.Zero, onChainCount);
        }

        [Fact]
        [Trait("UseCase", "Workflows")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#workflows-uninstall")]
        public async Task Given_an_installed_executor_When_uninstalling_it_Then_isModuleInstalled_becomes_false()
        {
            await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<WorkflowsViewModel>();
            await vm.CreateAgentCommand.ExecuteAsync(null);
            await vm.InstallExecutorCommand.ExecuteAsync(null);
            Assert.True(vm.IsExecutorInstalled, vm.ErrorMessage);

            await vm.UninstallExecutorCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.False(vm.IsExecutorInstalled);
        }

        [Theory]
        [Trait("UseCase", "Workflows")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#workflows-guards")]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Given_no_active_account_or_installed_executor_When_a_workflows_command_runs_Then_a_readable_error_is_surfaced(int commandIndex)
        {
            var session = new SessionState();
            _fixture.Infra.PublishInto(session);

            var vm = new WorkflowsViewModel(session);

            var isBusyTransitions = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WorkflowsViewModel.IsBusy))
                    isBusyTransitions.Add(vm.IsBusy);
            };

            switch (commandIndex)
            {
                case 0:
                    await vm.InstallExecutorCommand.ExecuteAsync(null);
                    break;
                case 1:
                    await vm.RunDelegatedActionCommand.ExecuteAsync(null);
                    break;
                case 2:
                    await vm.TryUnauthorizedActionCommand.ExecuteAsync(null);
                    break;
                default:
                    await vm.UninstallExecutorCommand.ExecuteAsync(null);
                    break;
            }

            Assert.Equal(new[] { true, false }, isBusyTransitions);
            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        }
    }
}
