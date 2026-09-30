using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    [Collection(EnterpriseDemoCollection.COLLECTION_NAME)]
    public class TieredApprovalViewModelTests
    {
        private readonly EnterpriseDemoFixture _fixture;

        public TieredApprovalViewModelTests(EnterpriseDemoFixture fixture)
        {
            _fixture = fixture;
        }

        private static readonly string PolicyViolationSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("PolicyViolation(bytes32,address)"))[..4].ToHex(true);

        private static readonly string InvalidSignatureSelector = Sha3Keccack.Current
            .CalculateHash(Encoding.UTF8.GetBytes("InvalidSignature()"))[..4].ToHex(true);

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
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_an_enrolled_account_When_the_owner_installs_tiered_roles_Then_both_bands_permissionIds_and_addresses_are_recorded()
        {
            var session = await NewEnrolledAndFundedSessionAsync("wendy");
            var vm = new TieredApprovalViewModel(session) { SmallCap = 100, LargeCap = 1000 };

            await vm.InstallTier2RoleCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsInstalled, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.OperatorAddress));
            Assert.False(string.IsNullOrEmpty(vm.Tier1PermissionIdHex));
            Assert.False(string.IsNullOrEmpty(vm.Tier2PermissionIdHex));
            Assert.Equal(3, vm.MemberAddresses.Count);

            var progress = session.RequireTieredRoles("wendy");
            Assert.Equal(vm.OperatorAddress, progress.OperatorAddress);
            Assert.Equal(new BigInteger(100), progress.SmallCap);
            Assert.Equal(new BigInteger(1000), progress.LargeCap);
            Assert.Equal(3, progress.MemberKeys.Count);
            Assert.Equal(2, progress.Threshold);
            Assert.False(progress.IsQuorumPaid);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_tiered_roles_When_a_2_of_3_member_quorum_pays_within_the_tier2_cap_Then_the_deposit_executes_and_the_target_balance_grows()
        {
            var session = await NewEnrolledAndFundedSessionAsync("xavier");
            var vm = new TieredApprovalViewModel(session) { SmallCap = 100, LargeCap = 1000, QuorumPayAmount = 600 };
            await vm.InstallTier2RoleCommand.ExecuteAsync(null);
            Assert.True(vm.IsInstalled, vm.ErrorMessage ?? vm.StatusMessage);

            var balanceBefore = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await vm.PayWithQuorumCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.NotNull(vm.LastReceipt);
            Assert.True(vm.LastReceipt!.UserOpSuccess, vm.LastReceipt.FailureDiagnostic);
            Assert.True(vm.IsQuorumPaid, vm.StatusMessage);
            Assert.Equal(balanceBefore.Value + 600, vm.TargetBalance);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(vm.TargetBalance, onChainBalance.Value);

            var progress = session.RequireTieredRoles("xavier");
            Assert.True(progress.IsQuorumPaid);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_tiered_roles_When_only_1_of_3_members_signs_Then_it_is_rejected_by_the_authority_with_the_InvalidSignature_marker_and_balance_unchanged()
        {
            var session = await NewEnrolledAndFundedSessionAsync("yusuf");
            var vm = new TieredApprovalViewModel(session) { SmallCap = 100, LargeCap = 1000, QuorumPayAmount = 600 };
            await vm.InstallTier2RoleCommand.ExecuteAsync(null);
            Assert.True(vm.IsInstalled, vm.ErrorMessage ?? vm.StatusMessage);
            var balanceBeforeAttempt = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await vm.TryUnderQuorumCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.UnderQuorumRejected == true, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
            Assert.Contains(InvalidSignatureSelector, vm.StatusMessage!);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(balanceBeforeAttempt.Value, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_tiered_roles_When_a_genuine_quorum_pays_over_the_tier2_cap_Then_it_is_rejected_by_the_cap_policy_with_no_InvalidSignature_marker_and_balance_unchanged()
        {
            var session = await NewEnrolledAndFundedSessionAsync("zoe");
            var vm = new TieredApprovalViewModel(session) { SmallCap = 100, LargeCap = 1000, OverCapAmount = 1500 };
            await vm.InstallTier2RoleCommand.ExecuteAsync(null);
            Assert.True(vm.IsInstalled, vm.ErrorMessage ?? vm.StatusMessage);
            var balanceBeforeAttempt = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await vm.TryOverCapCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.OverCapRejected == true, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
            Assert.Contains(PolicyViolationSelector, vm.StatusMessage!);
            Assert.DoesNotContain(InvalidSignatureSelector, vm.StatusMessage!);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(balanceBeforeAttempt.Value, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_tiered_roles_When_the_tier1_operator_key_attempts_a_tier2_sized_value_Then_it_is_rejected_by_tier1s_own_cap_policy_and_balance_unchanged()
        {
            var session = await NewEnrolledAndFundedSessionAsync("amir");
            var vm = new TieredApprovalViewModel(session) { SmallCap = 100, LargeCap = 1000, QuorumPayAmount = 600 };
            await vm.InstallTier2RoleCommand.ExecuteAsync(null);
            Assert.True(vm.IsInstalled, vm.ErrorMessage ?? vm.StatusMessage);
            var balanceBeforeAttempt = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await vm.TryTier1OverItsTierCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.True(vm.Tier1OverItsTierRejected == true, vm.StatusMessage);
            Assert.False(string.IsNullOrEmpty(vm.StatusMessage));
            Assert.Contains(PolicyViolationSelector, vm.StatusMessage!);

            var onChainBalance = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);
            Assert.Equal(balanceBeforeAttempt.Value, onChainBalance.Value);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_tiered_roles_have_not_been_installed_When_paying_with_a_quorum_is_called_Then_a_readable_error_is_surfaced()
        {
            var session = await NewEnrolledAndFundedSessionAsync("bianca");
            var vm = new TieredApprovalViewModel(session);

            await vm.PayWithQuorumCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.IsQuorumPaid);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseTieredApproval")]
        public async Task Given_no_active_user_When_installing_tiered_roles_Then_a_readable_error_is_surfaced()
        {
            var session = _fixture.NewReadySession();
            var vm = new TieredApprovalViewModel(session);

            await vm.InstallTier2RoleCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.False(vm.IsInstalled);
        }
    }
}
