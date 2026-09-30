using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(PasskeyHostFixture.COLLECTION_NAME)]
    public class SocialRecoveryViewModelTests
    {
        private readonly PasskeyHostFixture _fixture;

        public SocialRecoveryViewModelTests(PasskeyHostFixture fixture)
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
        [Trait("UseCase", "SocialRecovery")]
        [NethereumDocExample(DocSection.AccountAbstraction, "social-recovery", "Configure an N-of-M guardian quorum - install SocialRecovery on the account's first-ever UserOp", Order = 1)]
        public async Task Given_a_funded_ecdsa_account_When_setting_up_guardians_Then_isModuleInstalled_becomes_true()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<SocialRecoveryViewModel>();

            await vm.SetupGuardiansCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsGuardiansInstalled);
            Assert.NotNull(vm.GuardianAddresses);
            Assert.Equal(vm.GuardianCount, vm.GuardianAddresses!.Length);
            Assert.Equal(vm.SocialRecoveryAddress, _fixture.Infra.SocialRecoveryAddress);

            var code = await _fixture.Infra.Bootstrap.Node.GetCodeAsync(session.Account!.Address);
            Assert.NotNull(code);
            Assert.True(code.Length > 0);
        }

        [Fact]
        [Trait("UseCase", "SocialRecovery")]
        [NethereumDocExample(DocSection.AccountAbstraction, "social-recovery", "A guardian quorum recovers the account - the owner rotates without the old or new owner ever signing", Order = 2)]
        public async Task Given_installed_guardians_When_a_quorum_recovers_Then_the_owner_rotates_to_the_new_key()
        {
            await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<SocialRecoveryViewModel>();
            await vm.SetupGuardiansCommand.ExecuteAsync(null);
            Assert.True(vm.IsGuardiansInstalled, vm.ErrorMessage);

            await vm.RecoverCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.False(string.IsNullOrEmpty(vm.NewOwnerAddress));
            Assert.Equal(vm.NewOwnerAddress!.ToLowerInvariant(), vm.RotatedOwnerAddress!.ToLowerInvariant());
        }

        [Fact]
        [Trait("UseCase", "SocialRecovery")]
        public async Task Given_a_completed_recovery_When_switching_to_the_new_owner_Then_it_genuinely_controls_the_account()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<SocialRecoveryViewModel>();
            await vm.SetupGuardiansCommand.ExecuteAsync(null);
            await vm.RecoverCommand.ExecuteAsync(null);
            Assert.False(string.IsNullOrEmpty(vm.NewOwnerAddress), vm.ErrorMessage);

            var accountAddress = session.Account!.Address;

            await vm.SwitchToRecoveredOwnerCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsRecoveredAccountActive);
            Assert.Equal(BigInteger.One, vm.ProvedCount);

            Assert.Equal(accountAddress, session.Account.Address);
            Assert.Contains("Recovered", session.AccountDescription);

            var count = await _fixture.Infra.TestCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("UseCase", "SocialRecovery")]
        [NethereumDocExample(DocSection.AccountAbstraction, "social-recovery", "Under-threshold rejection - an (M-1)-of-M quorum is rejected on-chain and the owner stays unchanged", Order = 3)]
        public async Task Given_installed_guardians_When_an_under_threshold_quorum_attempts_recovery_Then_it_is_rejected_and_the_owner_is_unchanged()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<SocialRecoveryViewModel>();
            await vm.SetupGuardiansCommand.ExecuteAsync(null);
            Assert.True(vm.IsGuardiansInstalled, vm.ErrorMessage);

            var ecdsaValidator = new Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ECDSAValidatorService(
                _fixture.Infra.Bootstrap.OperatorWeb3, _fixture.Infra.DeploymentAddresses.EcdsaValidatorAddress);
            var ownerBefore = await ecdsaValidator.GetOwnerQueryAsync(session.Account!.Address);

            await vm.TryUnderThresholdRecoveryCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.UnderThresholdRejected == true, vm.StatusMessage);
            Assert.Contains("AA23", vm.StatusMessage);

            var ownerAfter = await ecdsaValidator.GetOwnerQueryAsync(session.Account.Address);
            Assert.Equal(ownerBefore.ToLowerInvariant(), ownerAfter.ToLowerInvariant());
        }

        [Theory]
        [Trait("UseCase", "SocialRecovery")]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task Given_no_active_account_or_guardians_When_a_social_recovery_command_runs_Then_a_readable_error_is_surfaced(int commandIndex)
        {
            var session = new SessionState();
            _fixture.Infra.PublishInto(session);

            var vm = new SocialRecoveryViewModel(session);

            var isBusyTransitions = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SocialRecoveryViewModel.IsBusy))
                    isBusyTransitions.Add(vm.IsBusy);
            };

            switch (commandIndex)
            {
                case 0:
                    await vm.SetupGuardiansCommand.ExecuteAsync(null);
                    break;
                case 1:
                    await vm.RecoverCommand.ExecuteAsync(null);
                    break;
                case 2:
                    await vm.SwitchToRecoveredOwnerCommand.ExecuteAsync(null);
                    break;
                default:
                    await vm.TryUnderThresholdRecoveryCommand.ExecuteAsync(null);
                    break;
            }

            Assert.Equal(new[] { true, false }, isBusyTransitions);
            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        }
    }
}
