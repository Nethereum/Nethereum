using System;
using System.Linq;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    [Collection(EnterpriseDemoCollection.COLLECTION_NAME)]
    public class EnterpriseAdminViewModelTests
    {
        private readonly EnterpriseDemoFixture _fixture;

        public EnterpriseAdminViewModelTests(EnterpriseDemoFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("UseCase", "EnterpriseAdmin")]
        public async Task Given_a_provisioned_stack_When_the_admin_enrolls_a_userId_Then_the_account_is_deployed_active_and_listed_in_the_directory()
        {
            var session = _fixture.NewReadySession();
            var vm = new EnterpriseAdminViewModel(session) { UserId = "alice" };

            await vm.EnrollCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.False(string.IsNullOrEmpty(vm.ResolvedAddress));
            Assert.True(vm.IsActive == true, vm.StatusMessage);
            Assert.True(await session.AdminService!.IsAccountDeployedAsync(vm.ResolvedAddress!));
            Assert.True(await session.AdminService!.IsActiveAsync(vm.ResolvedAddress!));

            Assert.Single(vm.Directory);
            var entry = vm.Directory[0];
            Assert.Equal("alice", entry.UserId);
            Assert.Equal(vm.ResolvedAddress, entry.AccountAddress);
            Assert.True(entry.IsActive);
            Assert.True(entry.IsSelected);

            Assert.True(session.TryGetEnrolledUser("alice", out var enrolled));
            Assert.Equal(vm.ResolvedAddress, enrolled!.AccountAddress);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseAdmin")]
        public async Task Given_no_active_user_When_a_userId_is_created_Then_it_becomes_the_active_user()
        {
            var session = _fixture.NewReadySession();
            Assert.Null(session.ActiveUserId);
            var vm = new EnterpriseAdminViewModel(session) { UserId = "flynn" };

            await vm.EnrollCommand.ExecuteAsync(null);

            Assert.Null(vm.ErrorMessage);
            Assert.Equal("flynn", session.ActiveUserId);
            Assert.Equal("flynn", vm.ActiveUserId);
            Assert.Null(vm.UserId);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseAdmin")]
        public async Task Given_two_userIds_When_both_are_enrolled_Then_the_directory_lists_both_with_distinct_addresses()
        {
            var session = _fixture.NewReadySession();
            var vm = new EnterpriseAdminViewModel(session);

            vm.UserId = "bob";
            await vm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            var bobAddress = vm.ResolvedAddress;

            vm.UserId = "carol";
            await vm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            var carolAddress = vm.ResolvedAddress;

            Assert.NotEqual(bobAddress, carolAddress);
            Assert.Equal(2, vm.Directory.Count);

            Assert.Equal("carol", session.ActiveUserId);
            Assert.True(vm.Directory.Single(u => u.UserId == "carol").IsSelected);
            Assert.False(vm.Directory.Single(u => u.UserId == "bob").IsSelected);

            await vm.SelectUserCommand.ExecuteAsync("bob");

            Assert.Null(vm.ErrorMessage);
            Assert.Equal("bob", session.ActiveUserId);
            Assert.Equal("bob", vm.ActiveUserId);
            Assert.True(vm.Directory.Single(u => u.UserId == "bob").IsSelected);
            Assert.False(vm.Directory.Single(u => u.UserId == "carol").IsSelected);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseAdmin")]
        public async Task Given_a_userId_already_enrolled_When_enrolling_it_again_Then_a_readable_error_is_surfaced()
        {
            var session = _fixture.NewReadySession();
            var vm = new EnterpriseAdminViewModel(session) { UserId = "dave" };
            await vm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);

            vm.UserId = "dave";
            await vm.EnrollCommand.ExecuteAsync(null);

            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.Contains("already enrolled", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Single(vm.Directory);
        }

        [Fact]
        [Trait("UseCase", "EnterpriseAdmin")]
        public async Task Given_infrastructure_not_provisioned_When_enrolling_Then_a_readable_error_is_surfaced_and_nothing_throws()
        {
            var vm = new EnterpriseAdminViewModel(new SessionState()) { UserId = "erin" };

            await vm.EnrollCommand.ExecuteAsync(null);

            Assert.False(vm.IsBusy);
            Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
            Assert.Empty(vm.Directory);
        }
    }
}
