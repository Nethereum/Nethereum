using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    [Collection(PasskeyHostFixture.COLLECTION_NAME)]
    public class PasskeyAccountViewModelTests
    {
        private readonly PasskeyHostFixture _fixture;

        public PasskeyAccountViewModelTests(PasskeyHostFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("UseCase", "Passkey")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#passkey-account")]
        public async Task Given_a_configured_host_When_the_user_creates_a_passkey_account_Then_a_counterfactual_smart_account_address_is_shown()
        {
            var vm = _fixture.Services.GetRequiredService<PasskeyAccountViewModel>();
            var session = _fixture.Services.GetRequiredService<SessionState>();

            await vm.CreateAccountCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.False(string.IsNullOrEmpty(vm.SmartAccountAddress));
            Assert.False(string.IsNullOrEmpty(vm.CredentialId));
            Assert.False(vm.IsDeployed);
            Assert.NotNull(session.Account);
            Assert.Equal(vm.SmartAccountAddress, session.Account!.Address);
            Assert.Equal("Passkey · WebAuthn P-256", session.AccountDescription);
        }

        [Fact]
        [Trait("UseCase", "Passkey")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#fund-account")]
        public async Task Given_a_freshly_created_passkey_account_When_the_user_funds_it_Then_the_faucet_reports_a_positive_balance()
        {
            var vm = _fixture.Services.GetRequiredService<PasskeyAccountViewModel>();
            var session = _fixture.Services.GetRequiredService<SessionState>();
            var faucet = _fixture.Services.GetRequiredService<IDevChainFaucet>();

            await vm.CreateAccountCommand.ExecuteAsync(null);
            var balanceBefore = await faucet.GetBalanceAsync(session.Account!.Address);
            Assert.Equal(BigInteger.Zero, balanceBefore);

            await vm.FundAccountCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            var balanceAfter = await faucet.GetBalanceAsync(session.Account.Address);
            Assert.True(balanceAfter > BigInteger.Zero);
        }

        [Fact]
        [Trait("UseCase", "Passkey")]
        [Trait("Doc", "docs/aa/example-viewmodels.md#passkey-send")]
        public async Task Given_a_funded_passkey_account_When_the_user_sends_an_operation_Then_it_deploys_and_the_passkey_signed_call_succeeds()
        {
            var vm = _fixture.Services.GetRequiredService<PasskeyAccountViewModel>();
            var session = _fixture.Services.GetRequiredService<SessionState>();

            await vm.CreateAccountCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            await _fixture.Infra.FundAsync(session.Account!.Address);

            await vm.SendCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.Equal(BigInteger.One, vm.Count);

            var codeAfterDeploy = await _fixture.Infra.Bootstrap.Node.GetCodeAsync(session.Account.Address);
            Assert.NotNull(codeAfterDeploy);
            Assert.True(codeAfterDeploy.Length > 0);
        }
    }
}
