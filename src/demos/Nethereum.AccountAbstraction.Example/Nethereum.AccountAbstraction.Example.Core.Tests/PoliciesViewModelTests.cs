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
    public class PoliciesViewModelTests
    {
        private readonly PasskeyHostFixture _fixture;

        public PoliciesViewModelTests(PasskeyHostFixture fixture)
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
        [Trait("UseCase", "Policies")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#policies-create")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-sessions-and-policies", "Session lifecycle: enable - install SmartSession and enable a policy-scoped session in one UserOp", Order = 1)]
        public async Task Given_a_funded_ecdsa_account_When_creating_a_scoped_session_key_Then_the_session_is_enabled()
        {
            await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<PoliciesViewModel>();

            await vm.CreateScopedSessionKeyCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.HasScopedSession);
            Assert.False(string.IsNullOrEmpty(vm.SessionKeyAddress));
            Assert.False(string.IsNullOrEmpty(vm.PolicySummary));
        }

        [Fact]
        [Trait("UseCase", "Policies")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#policies-use")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-sessions-and-policies", "Session lifecycle: use - the session key (not the account owner) signs an in-policy call", Order = 2)]
        public async Task Given_a_scoped_session_key_When_using_it_Then_count_succeeds_and_the_counter_increments()
        {
            var session = await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<PoliciesViewModel>();
            await vm.CreateScopedSessionKeyCommand.ExecuteAsync(null);
            Assert.True(vm.HasScopedSession, vm.ErrorMessage);

            await vm.UseSessionKeyCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.Equal(BigInteger.One, vm.Count);

            var onChainCount = await _fixture.Infra.TestCounter.CountersQueryAsync(session.Account!.Address);
            Assert.Equal(BigInteger.One, onChainCount);
        }

        [Fact]
        [Trait("UseCase", "Policies")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#policies-out-of-policy")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-sessions-and-policies", "Session lifecycle: out-of-policy rejection - a call beyond the UniActionPolicy cap is rejected on-chain", Order = 3)]
        public async Task Given_a_scoped_session_key_When_exceeding_the_UniActionPolicy_cap_Then_it_is_rejected()
        {
            await NewFundedEcdsaAccountAsync();
            var vm = _fixture.Services.GetRequiredService<PoliciesViewModel>();
            await vm.CreateScopedSessionKeyCommand.ExecuteAsync(null);
            Assert.True(vm.HasScopedSession, vm.ErrorMessage);

            await vm.TryOutOfPolicyActionCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.OutOfPolicyRejected == true, vm.StatusMessage);
        }

        [Theory]
        [Trait("UseCase", "Policies")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#policies-guards")]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task Given_no_active_account_or_session_When_a_policies_command_runs_Then_a_readable_error_is_surfaced(int commandIndex)
        {
            var session = new SessionState();
            _fixture.Infra.PublishInto(session);

            var vm = new PoliciesViewModel(session);

            var isBusyTransitions = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PoliciesViewModel.IsBusy))
                    isBusyTransitions.Add(vm.IsBusy);
            };

            switch (commandIndex)
            {
                case 0:
                    await vm.CreateScopedSessionKeyCommand.ExecuteAsync(null);
                    break;
                case 1:
                    await vm.UseSessionKeyCommand.ExecuteAsync(null);
                    break;
                default:
                    await vm.TryOutOfPolicyActionCommand.ExecuteAsync(null);
                    break;
            }

            Assert.Equal(new[] { true, false }, isBusyTransitions);
            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
        }
    }
}
