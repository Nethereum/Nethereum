using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    [Collection(EnterpriseDemoCollection.COLLECTION_NAME)]
    public class EnterpriseOperatorViewModelTests
    {
        private readonly EnterpriseDemoFixture _fixture;

        public EnterpriseOperatorViewModelTests(EnterpriseDemoFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<SessionState> NewEnrolledAndFundedSessionAsync(string userId)
        {
            var session = _fixture.NewReadySession();
            var adminVm = new EnterpriseAdminViewModel(session) { UserId = userId };
            await adminVm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(adminVm.ErrorMessage);

            await _fixture.FundAsync(adminVm.ResolvedAddress!);
            return session;
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_an_enrolled_account_When_the_owner_installs_a_capped_role_Then_the_session_key_and_permissionId_are_recorded()
        {
            var session = await NewEnrolledAndFundedSessionAsync("frank");
            var vm = new EnterpriseOperatorViewModel(session) { Cap = 100 };

            await vm.InstallCappedRoleCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.HasCappedRole);
            Assert.False(string.IsNullOrEmpty(vm.SessionKeyAddress));
            Assert.False(string.IsNullOrEmpty(vm.PermissionIdHex));

            var role = session.RequireCappedRole("frank");
            Assert.Equal(vm.SessionKeyAddress, role.SessionKeyAddress);
            Assert.Equal(new BigInteger(100), role.Cap);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_a_capped_role_When_paying_within_cap_Then_the_deposit_executes_and_the_target_balance_grows()
        {
            var session = await NewEnrolledAndFundedSessionAsync("grace");
            var vm = new EnterpriseOperatorViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.InstallCappedRoleCommand.ExecuteAsync(null);
            Assert.True(vm.HasCappedRole, vm.ErrorMessage);

            var balanceBefore = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await vm.PayWithinCapCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.Equal(balanceBefore.Value + 60, vm.TargetBalance);
            Assert.True(vm.OverCapRejected is null);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(vm.TargetBalance, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_a_capped_role_When_paying_over_cap_Then_it_is_rejected_on_chain_and_surfaced_on_the_view_model()
        {
            var session = await NewEnrolledAndFundedSessionAsync("henry");
            var vm = new EnterpriseOperatorViewModel(session) { Cap = 100, WithinCapAmount = 60, OverCapAmount = 150 };
            await vm.InstallCappedRoleCommand.ExecuteAsync(null);
            Assert.True(vm.HasCappedRole, vm.ErrorMessage);

            await vm.PayWithinCapCommand.ExecuteAsync(null);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.ErrorMessage);
            var balanceAfterWithinCap = vm.TargetBalance;

            await vm.TryOverCapCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.OverCapRejected == true, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
            Assert.Contains("PolicyViolation", vm.StatusMessage!);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(balanceAfterWithinCap, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_no_active_user_When_installing_a_capped_role_Then_a_readable_error_is_surfaced()
        {
            var session = _fixture.NewReadySession();
            var vm = new EnterpriseOperatorViewModel(session);

            await vm.InstallCappedRoleCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.HasCappedRole);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_a_capped_role_When_the_cap_is_edited_higher_Then_a_previously_rejected_pay_now_executes()
        {
            var session = _fixture.NewReadySession();
            var adminVm = new EnterpriseAdminViewModel(session) { UserId = "wendy" };
            await adminVm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(adminVm.ErrorMessage);

            var vm = new EnterpriseOperatorViewModel(session) { Cap = 100, OverCapAmount = 150 };
            await vm.InstallCappedRoleCommand.ExecuteAsync(null);
            Assert.True(vm.HasCappedRole, vm.ErrorMessage ?? vm.StatusMessage);
            var originalSessionKeyAddress = vm.SessionKeyAddress;
            var originalPermissionIdHex = vm.PermissionIdHex;

            await vm.TryOverCapCommand.ExecuteAsync(null);
            Assert.True(vm.OverCapRejected == true, vm.StatusMessage);
            Assert.Contains("PolicyViolation", vm.StatusMessage!);

            vm.NewCap = 1000;
            await vm.EditCapCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.HasCappedRole, vm.StatusMessage);
            Assert.Equal(new BigInteger(1000), vm.Cap);
            Assert.False(string.IsNullOrEmpty(vm.SessionKeyAddress));
            Assert.False(string.IsNullOrEmpty(vm.PermissionIdHex));
            Assert.NotEqual(originalSessionKeyAddress, vm.SessionKeyAddress);
            Assert.NotEqual(originalPermissionIdHex, vm.PermissionIdHex);

            var role = session.RequireCappedRole("wendy");
            Assert.Equal(new BigInteger(1000), role.Cap);
            Assert.Equal(vm.SessionKeyAddress, role.SessionKeyAddress);

            vm.WithinCapAmount = 150;
            var balanceBeforeRaisedPay = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await vm.PayWithinCapCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.Equal(balanceBeforeRaisedPay.Value + 150, vm.TargetBalance);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(vm.TargetBalance, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_no_active_user_When_editing_the_cap_Then_a_readable_error_is_surfaced()
        {
            var session = _fixture.NewReadySession();
            var vm = new EnterpriseOperatorViewModel(session) { NewCap = 500 };

            await vm.EditCapCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.HasCappedRole);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_a_capped_role_When_revoked_Then_the_previously_live_session_key_is_rejected_InvalidPermissionId_and_the_role_is_gone()
        {
            var session = _fixture.NewReadySession();
            var adminVm = new EnterpriseAdminViewModel(session) { UserId = "xena" };
            await adminVm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(adminVm.ErrorMessage);

            var vm = new EnterpriseOperatorViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.InstallCappedRoleCommand.ExecuteAsync(null);
            Assert.True(vm.HasCappedRole, vm.ErrorMessage ?? vm.StatusMessage);

            await vm.PayWithinCapCommand.ExecuteAsync(null);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.ErrorMessage ?? vm.StatusMessage);
            var balanceAfterLivePay = vm.TargetBalance;

            await vm.RevokeRoleCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.False(vm.HasCappedRole, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
            Assert.Contains("InvalidPermissionId", vm.StatusMessage!);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);

            var user = session.RequireEnrolledUser("xena");
            var accountService = new Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.NethereumAccountService(
                session.Web3!, user.AccountAddress);
            var stillInstalled = await accountService.IsModuleInstalledQueryAsync(
                Nethereum.AccountAbstraction.ERC7579.ERC7579ModuleTypes.TYPE_VALIDATOR,
                session.Deployment!.Modules.SmartSession,
                Array.Empty<byte>());
            Assert.False(stillInstalled);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(balanceAfterLivePay, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOperator")]
        public async Task Given_no_active_user_When_revoking_the_role_Then_a_readable_error_is_surfaced()
        {
            var session = _fixture.NewReadySession();
            var vm = new EnterpriseOperatorViewModel(session);

            await vm.RevokeRoleCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.HasCappedRole);
        }
    }
}
