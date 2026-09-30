using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(PasskeyHostFixture.COLLECTION_NAME)]
    public class ModulesViewModelTests
    {
        private readonly PasskeyHostFixture _fixture;

        public ModulesViewModelTests(PasskeyHostFixture fixture)
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
        [Trait("UseCase", "Modules")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#modules-install")]
        public async Task Given_a_funded_ecdsa_account_When_installing_the_passkey_validator_Then_isModuleInstalled_becomes_true()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<ModulesViewModel>();

            await vm.InstallPasskeyValidatorCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsPasskeyValidatorInstalled);
            Assert.False(string.IsNullOrEmpty(vm.CredentialId));
            Assert.Equal(_fixture.Infra.WebAuthnValidatorAddress, vm.ModuleAddress);

            var code = await _fixture.Infra.Bootstrap.Node.GetCodeAsync(session.Account!.Address);
            Assert.NotNull(code);
            Assert.True(code.Length > 0);
        }

        [Fact]
        [Trait("UseCase", "Modules")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#modules-prove")]
        public async Task Given_an_installed_passkey_validator_When_proving_it_can_sign_Then_a_passkey_signed_operation_succeeds()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<ModulesViewModel>();
            await vm.InstallPasskeyValidatorCommand.ExecuteAsync(null);
            Assert.True(vm.IsPasskeyValidatorInstalled, vm.ErrorMessage);

            await vm.ProvePasskeyCanSignCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);

            var count = await _fixture.Infra.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("UseCase", "Modules")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#modules-uninstall")]
        public async Task Given_an_installed_passkey_validator_When_uninstalling_it_Then_isModuleInstalled_becomes_false()
        {
            await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<ModulesViewModel>();
            await vm.InstallPasskeyValidatorCommand.ExecuteAsync(null);
            Assert.True(vm.IsPasskeyValidatorInstalled, vm.ErrorMessage);

            await vm.UninstallPasskeyValidatorCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.False(vm.IsPasskeyValidatorInstalled);
        }

        [Theory]
        [Trait("UseCase", "Modules")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#modules-guards")]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Given_no_active_account_When_a_module_command_runs_Then_a_readable_error_is_surfaced(int commandIndex)
        {
            var session = new SessionState();
            _fixture.Infra.PublishInto(session);

            var vm = new ModulesViewModel(
                session,
                _fixture.Services.GetRequiredService<Nethereum.WebAuthn.IWebAuthnCredentialFactory>(),
                _fixture.Services.GetRequiredService<Nethereum.WebAuthn.IWebAuthnAuthenticator>());

            var isBusyTransitions = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ModulesViewModel.IsBusy))
                    isBusyTransitions.Add(vm.IsBusy);
            };

            switch (commandIndex)
            {
                case 0:
                    await vm.InstallPasskeyValidatorCommand.ExecuteAsync(null);
                    break;
                case 1:
                    await vm.ProvePasskeyCanSignCommand.ExecuteAsync(null);
                    break;
                default:
                    await vm.UninstallPasskeyValidatorCommand.ExecuteAsync(null);
                    break;
            }

            Assert.Equal(new[] { true, false }, isBusyTransitions);
            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        }
    }
}
