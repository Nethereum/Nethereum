using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Registry
{
    [Collection(AAIntegrationFixture.COLLECTION_NAME)]
    public class AdminRoleManagementTests
    {
        private readonly AAIntegrationFixture _fixture;

        public AdminRoleManagementTests(AAIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task InitialAdmin_HasAdminRole()
        {
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();
            var hasRole = await _fixture.AccountRegistryService.HasRoleQueryAsync(
                adminRole, _fixture.OperatorAccount.Address);

            Assert.True(hasRole, "Initial admin should have admin role");
        }

        [Fact]
        public async Task InitialAdmin_HasDefaultAdminRole()
        {
            var defaultAdminRole = await _fixture.AccountRegistryService.DefaultAdminRoleQueryAsync();
            var hasRole = await _fixture.AccountRegistryService.HasRoleQueryAsync(
                defaultAdminRole, _fixture.OperatorAccount.Address);

            Assert.True(hasRole, "Initial admin should have default admin role");
        }

        [Fact]
        public async Task GrantAdminRole_ByExistingAdmin_Succeeds()
        {
            var newAdmin = _fixture.UserAccounts[0].Address;
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            var hasRoleBefore = await _fixture.AccountRegistryService.HasRoleQueryAsync(adminRole, newAdmin);
            Assert.False(hasRoleBefore, "New admin should not have role yet");

            await _fixture.AccountRegistryService.GrantRoleRequestAndWaitForReceiptAsync(adminRole, newAdmin);

            var hasRoleAfter = await _fixture.AccountRegistryService.HasRoleQueryAsync(adminRole, newAdmin);
            Assert.True(hasRoleAfter, "New admin should have role after grant");
        }

        [Fact]
        public async Task GrantAdminRole_ByNonAdmin_Fails()
        {
            var nonAdmin = _fixture.UserAccounts[1];
            var newAdmin = _fixture.UserAccounts[2].Address;
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            var nonAdminService = _fixture.GetAccountRegistryServiceForAccount(nonAdmin);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonAdminService.GrantRoleRequestAndWaitForReceiptAsync(adminRole, newAdmin));
        }

        [Fact]
        public async Task RevokeAdminRole_ByAdmin_Succeeds()
        {
            var adminToRevoke = _fixture.UserAccounts[3].Address;
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            await _fixture.AccountRegistryService.GrantRoleRequestAndWaitForReceiptAsync(adminRole, adminToRevoke);
            var hasRoleBefore = await _fixture.AccountRegistryService.HasRoleQueryAsync(adminRole, adminToRevoke);
            Assert.True(hasRoleBefore);

            await _fixture.AccountRegistryService.RevokeRoleRequestAndWaitForReceiptAsync(adminRole, adminToRevoke);

            var hasRoleAfter = await _fixture.AccountRegistryService.HasRoleQueryAsync(adminRole, adminToRevoke);
            Assert.False(hasRoleAfter, "Admin role should be revoked");
        }

        [Fact]
        public async Task RevokeAdminRole_ByNonAdmin_Fails()
        {
            var nonAdmin = _fixture.UserAccounts[1];
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            var nonAdminService = _fixture.GetAccountRegistryServiceForAccount(nonAdmin);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonAdminService.RevokeRoleRequestAndWaitForReceiptAsync(
                    adminRole, _fixture.OperatorAccount.Address));
        }

        [Fact]
        public async Task NewAdmin_CanPerformAdminActions()
        {
            var newAdmin = _fixture.UserAccounts[4];
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            await _fixture.AccountRegistryService.GrantRoleRequestAndWaitForReceiptAsync(adminRole, newAdmin.Address);

            var newAdminService = _fixture.GetAccountRegistryServiceForAccount(newAdmin);
            var accountToBan = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToBan);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(accountToBan);

            await newAdminService.BanRequestAndWaitForReceiptAsync(accountToBan, "Admin action test");

            var status = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToBan);
            Assert.Equal(3, status);
        }

        [Fact]
        public async Task RevokedAdmin_CannotPerformAdminActions()
        {
            var revokedAdmin = _fixture.BundlerAccount;
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            await _fixture.AccountRegistryService.GrantRoleRequestAndWaitForReceiptAsync(adminRole, revokedAdmin.Address);
            await _fixture.AccountRegistryService.RevokeRoleRequestAndWaitForReceiptAsync(adminRole, revokedAdmin.Address);

            var revokedAdminService = _fixture.GetAccountRegistryServiceForAccount(revokedAdmin);
            var accountToBan = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToBan);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => revokedAdminService.BanRequestAndWaitForReceiptAsync(accountToBan, "Should fail"));
        }

        [Fact]
        public async Task HasRole_ReturnsTrueForAdmins()
        {
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();

            var hasRole = await _fixture.AccountRegistryService.HasRoleQueryAsync(
                adminRole, _fixture.OperatorAccount.Address);

            Assert.True(hasRole);
        }

        [Fact]
        public async Task HasRole_ReturnsFalseForNonAdmins()
        {
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();
            var nonAdmin = "0xcccccccccccccccccccccccccccccccccccccccc";

            var hasRole = await _fixture.AccountRegistryService.HasRoleQueryAsync(adminRole, nonAdmin);

            Assert.False(hasRole);
        }

        [Fact]
        public async Task OperatorRole_IsDifferentFromAdminRole()
        {
            var adminRole = await _fixture.AccountRegistryService.AdminRoleQueryAsync();
            var operatorRole = await _fixture.AccountRegistryService.OperatorRoleQueryAsync();

            Assert.NotEqual(adminRole.ToHex(), operatorRole.ToHex());
        }

        [Fact]
        public async Task InitialAdmin_HasOperatorRole()
        {
            var operatorRole = await _fixture.AccountRegistryService.OperatorRoleQueryAsync();
            var hasRole = await _fixture.AccountRegistryService.HasRoleQueryAsync(
                operatorRole, _fixture.OperatorAccount.Address);

            Assert.True(hasRole, "Initial admin should have operator role");
        }
    }
}
