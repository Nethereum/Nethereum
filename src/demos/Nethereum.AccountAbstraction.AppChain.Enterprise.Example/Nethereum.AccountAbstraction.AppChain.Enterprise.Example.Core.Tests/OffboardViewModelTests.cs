using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    [Collection(EnterpriseDemoCollection.COLLECTION_NAME)]
    public class OffboardViewModelTests
    {
        private readonly EnterpriseDemoFixture _fixture;

        public OffboardViewModelTests(EnterpriseDemoFixture fixture)
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
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_a_live_faithful_enrolled_account_no_double_funding_When_the_full_offboard_sequence_runs_Then_every_step_succeeds_and_the_account_is_fully_severed()
        {
            var session = _fixture.NewReadySession();
            var adminVm = new EnterpriseAdminViewModel(session) { UserId = "reprouser" };
            await adminVm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(adminVm.ErrorMessage);
            var user = session.RequireEnrolledUser("reprouser");

            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };

            await vm.SetUpAccountCommand.ExecuteAsync(null);
            Assert.True(vm.IsSetUp, $"SetUp failed. Error={vm.ErrorMessage} Status={vm.StatusMessage} Receipt={vm.LastReceipt?.FailureDiagnostic}");

            await vm.RotateOwnerCommand.ExecuteAsync(null);
            Assert.True(vm.IsRotated, $"RotateOwner failed. Error={vm.ErrorMessage} Status={vm.StatusMessage} Receipt={vm.LastReceipt?.FailureDiagnostic}");
            Assert.True(vm.OldOwnerRejected == true, vm.StatusMessage);
            Assert.Contains("AA24", vm.StatusMessage!);

            await vm.RevokeSessionCommand.ExecuteAsync(null);
            Assert.True(vm.IsSessionRevoked, $"RevokeSession failed. Error={vm.ErrorMessage} Status={vm.StatusMessage} Receipt={vm.LastReceipt?.FailureDiagnostic}");
            Assert.True(vm.RevokedSessionRejected == true, vm.StatusMessage);
            Assert.Contains("InvalidPermissionId", vm.StatusMessage!);

            await vm.UninstallModuleCommand.ExecuteAsync(null);
            Assert.True(vm.IsModuleUninstalled, $"UninstallModule failed. Error={vm.ErrorMessage} Status={vm.StatusMessage} Receipt={vm.LastReceipt?.FailureDiagnostic}");

            var accountService = new Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.NethereumAccountService(
                session.Web3!, user.AccountAddress);
            var stillInstalled = await accountService.IsModuleInstalledQueryAsync(
                Nethereum.AccountAbstraction.ERC7579.ERC7579ModuleTypes.TYPE_VALIDATOR,
                session.Deployment!.Modules.SmartSession,
                Array.Empty<byte>());
            Assert.False(stillInstalled);

            await vm.SweepCommand.ExecuteAsync(null);
            Assert.True(vm.IsSwept, $"Sweep failed. Error={vm.ErrorMessage} Status={vm.StatusMessage} Receipt={vm.LastReceipt?.FailureDiagnostic}");
            Assert.False(string.IsNullOrEmpty(vm.TreasuryAddress));
            Assert.True(vm.SweptAmount > 0, vm.StatusMessage);
            var treasuryBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(vm.TreasuryAddress!);
            Assert.Equal(vm.SweptAmount, treasuryBalance.Value);

            await vm.BanCommand.ExecuteAsync(null);
            Assert.True(vm.IsBanned, $"Ban failed. Error={vm.ErrorMessage} Status={vm.StatusMessage}");
            Assert.False(await session.AdminService!.IsActiveAsync(user.AccountAddress));
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_an_enrolled_account_When_setup_runs_Then_the_session_and_guardians_are_live_and_the_baseline_pay_executes()
        {
            var session = await NewEnrolledAndFundedSessionAsync("olivia");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };

            await vm.SetUpAccountCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.IsSetUp, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.SessionKeyAddress));
            Assert.False(string.IsNullOrEmpty(vm.PermissionIdHex));
            Assert.Equal(3, vm.GuardianAddresses.Count);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);

            var progress = session.RequireOffboardProgress("olivia");
            Assert.Equal(vm.SessionKeyAddress, progress.SessionKeyAddress);
            Assert.Equal(3, progress.GuardianKeys.Count);
            Assert.Equal(2, progress.GuardianThreshold);
            Assert.False(progress.IsRotated);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(vm.TargetBalance, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_a_live_session_When_the_owner_is_rotated_Then_the_new_owner_controls_the_account_and_the_old_owner_is_dead_AA24()
        {
            var session = await NewEnrolledAndFundedSessionAsync("peter");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.SetUpAccountCommand.ExecuteAsync(null);
            Assert.True(vm.IsSetUp, vm.ErrorMessage ?? vm.StatusMessage);

            await vm.RotateOwnerCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.IsRotated, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.NewOwnerAddress));
            Assert.True(vm.OldOwnerRejected == true, vm.StatusMessage);
            Assert.Contains("AA24", vm.StatusMessage!);

            var progress = session.RequireOffboardProgress("peter");
            Assert.True(progress.IsRotated);
            Assert.Equal(vm.NewOwnerAddress, progress.NewOwnerKey!.GetPublicAddress());

            var ecdsaValidator = new Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ECDSAValidatorService(
                session.Web3!, session.Deployment!.Modules.EcdsaValidator);
            var user = session.RequireEnrolledUser("peter");
            var onChainOwner = await ecdsaValidator.GetOwnerQueryAsync(user.AccountAddress);
            Assert.Equal(vm.NewOwnerAddress!.ToLowerInvariant(), onChainOwner.ToLowerInvariant());
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_a_rotated_owner_When_the_session_is_revoked_Then_the_previously_live_session_key_is_rejected_InvalidPermissionId_and_balance_unchanged()
        {
            var session = await NewEnrolledAndFundedSessionAsync("quinn");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.SetUpAccountCommand.ExecuteAsync(null);
            await vm.RotateOwnerCommand.ExecuteAsync(null);
            Assert.True(vm.IsRotated, vm.ErrorMessage ?? vm.StatusMessage);
            var balanceAfterSetup = vm.TargetBalance;

            await vm.RevokeSessionCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.IsSessionRevoked, vm.StatusMessage);
            Assert.True(vm.RevokedSessionRejected == true, vm.StatusMessage);
            Assert.Contains("InvalidPermissionId", vm.StatusMessage!);

            var progress = session.RequireOffboardProgress("quinn");
            Assert.True(progress.IsSessionRevoked);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(balanceAfterSetup, onChainBalance.Value);
            Assert.Equal(vm.TargetBalance, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_a_revoked_session_When_the_module_is_uninstalled_Then_IsModuleInstalled_is_false()
        {
            var session = await NewEnrolledAndFundedSessionAsync("rosa");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.SetUpAccountCommand.ExecuteAsync(null);
            await vm.RotateOwnerCommand.ExecuteAsync(null);
            await vm.RevokeSessionCommand.ExecuteAsync(null);
            Assert.True(vm.IsSessionRevoked, vm.ErrorMessage ?? vm.StatusMessage);

            await vm.UninstallModuleCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.IsModuleUninstalled, vm.StatusMessage);

            var user = session.RequireEnrolledUser("rosa");
            var accountService = new Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.NethereumAccountService(
                session.Web3!, user.AccountAddress);
            var stillInstalled = await accountService.IsModuleInstalledQueryAsync(
                Nethereum.AccountAbstraction.ERC7579.ERC7579ModuleTypes.TYPE_VALIDATOR,
                session.Deployment!.Modules.SmartSession,
                Array.Empty<byte>());
            Assert.False(stillInstalled);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_an_uninstalled_module_When_swept_Then_the_treasury_receives_the_remaining_balance()
        {
            var session = await NewEnrolledAndFundedSessionAsync("sam");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.SetUpAccountCommand.ExecuteAsync(null);
            await vm.RotateOwnerCommand.ExecuteAsync(null);
            await vm.RevokeSessionCommand.ExecuteAsync(null);
            await vm.UninstallModuleCommand.ExecuteAsync(null);
            Assert.True(vm.IsModuleUninstalled, vm.ErrorMessage ?? vm.StatusMessage);

            await vm.SweepCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.IsSwept, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.TreasuryAddress));
            Assert.True(vm.SweptAmount > 0, vm.StatusMessage);

            var treasuryBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(vm.TreasuryAddress!);
            Assert.Equal(vm.SweptAmount, treasuryBalance.Value);

            var user = session.RequireEnrolledUser("sam");
            var accountBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(user.AccountAddress);
            Assert.True(accountBalance.Value <= Nethereum.Web3.Web3.Convert.ToWei(0.1m),
                $"Expected the account balance to drop to (sponsored) or below (self-paid) the gas reserve after the sweep, but it was {accountBalance.Value}.");
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_a_swept_account_When_banned_Then_IsActive_is_false()
        {
            var session = await NewEnrolledAndFundedSessionAsync("tara");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.SetUpAccountCommand.ExecuteAsync(null);
            await vm.RotateOwnerCommand.ExecuteAsync(null);
            await vm.RevokeSessionCommand.ExecuteAsync(null);
            await vm.UninstallModuleCommand.ExecuteAsync(null);
            await vm.SweepCommand.ExecuteAsync(null);
            Assert.True(vm.IsSwept, vm.ErrorMessage ?? vm.StatusMessage);

            await vm.BanCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.IsBanned, vm.StatusMessage);

            var user = session.RequireEnrolledUser("tara");
            Assert.False(await session.AdminService!.IsActiveAsync(user.AccountAddress));
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_setup_has_not_run_When_rotate_is_called_Then_a_readable_error_is_surfaced()
        {
            var session = await NewEnrolledAndFundedSessionAsync("uma");
            var vm = new OffboardViewModel(session);

            await vm.RotateOwnerCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.IsRotated);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_setup_has_run_but_not_rotated_When_revoke_is_called_Then_a_readable_error_is_surfaced()
        {
            var session = await NewEnrolledAndFundedSessionAsync("victor");
            var vm = new OffboardViewModel(session) { Cap = 100, WithinCapAmount = 60 };
            await vm.SetUpAccountCommand.ExecuteAsync(null);
            Assert.True(vm.IsSetUp, vm.ErrorMessage ?? vm.StatusMessage);

            await vm.RevokeSessionCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.IsSessionRevoked);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseOffboard")]
        public async Task Given_no_active_user_When_setup_is_called_Then_a_readable_error_is_surfaced()
        {
            var session = _fixture.NewReadySession();
            var vm = new OffboardViewModel(session);

            await vm.SetUpAccountCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.IsSetUp);
        }
    }
}
