using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures;
using Nethereum.Contracts;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Paymaster
{
    [Collection(AAIntegrationFixture.COLLECTION_NAME)]
    public class PaymasterTests
    {
        private readonly AAIntegrationFixture _fixture;

        public PaymasterTests(AAIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SponsoredPaymaster_IsDeployed()
        {
            Assert.NotNull(_fixture.SponsoredPaymasterService);
            Assert.NotEmpty(_fixture.SponsoredPaymasterService.ContractAddress);
        }

        [Fact]
        public async Task GetEntryPoint_ReturnsConfiguredEntryPoint()
        {
            var entryPoint = await _fixture.SponsoredPaymasterService.EntryPointQueryAsync();

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.ToLower());
        }

        [Fact]
        public async Task GetOwner_ReturnsConfiguredOwner()
        {
            var owner = await _fixture.SponsoredPaymasterService.OwnerQueryAsync();

            Assert.Equal(_fixture.OperatorAccount.Address.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task GetAccountRegistry_ReturnsConfiguredRegistry()
        {
            var registry = await _fixture.SponsoredPaymasterService.AccountRegistryQueryAsync();

            Assert.Equal(
                _fixture.AccountRegistryService.ContractAddress.ToLower(),
                registry.ToLower());
        }

        [Fact]
        public async Task GetDeposit_ReturnsNonZero()
        {
            var deposit = await _fixture.SponsoredPaymasterService.GetDepositQueryAsync();

            Assert.True(deposit > BigInteger.Zero);
        }

        [Fact]
        public async Task MaxDailySponsorPerUser_ReturnsConfiguredValue()
        {
            var maxPerUser = await _fixture.SponsoredPaymasterService.MaxDailySponsorPerUserQueryAsync();

            Assert.True(maxPerUser > BigInteger.Zero);
        }

        [Fact]
        public async Task MaxTotalDailySponsorship_ReturnsConfiguredValue()
        {
            var maxTotal = await _fixture.SponsoredPaymasterService.MaxTotalDailySponsorshipQueryAsync();

            Assert.True(maxTotal > BigInteger.Zero);
        }

        [Fact]
        public async Task SponsorshipStats_ReturnsValidValues()
        {
            var stats = await _fixture.SponsoredPaymasterService.GetSponsorshipStatsQueryAsync();

            Assert.True(stats.TotalAllTime >= BigInteger.Zero);
            Assert.True(stats.TodayTotal >= BigInteger.Zero);
            Assert.True(stats.RemainingToday >= BigInteger.Zero);
        }

        [Fact]
        public async Task SetMaxDailySponsorPerUser_ByOwner_Updates()
        {
            var newMax = Nethereum.Web3.Web3.Convert.ToWei(2);
            await _fixture.SponsoredPaymasterService.SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(newMax);

            var maxPerUser = await _fixture.SponsoredPaymasterService.MaxDailySponsorPerUserQueryAsync();

            Assert.Equal(newMax, maxPerUser);

            await _fixture.SponsoredPaymasterService.SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(
                Nethereum.Web3.Web3.Convert.ToWei(1));
        }

        [Fact]
        public async Task SetMaxTotalDailySponsorship_ByOwner_Updates()
        {
            var newMax = Nethereum.Web3.Web3.Convert.ToWei(20);
            await _fixture.SponsoredPaymasterService.SetMaxTotalDailySponsorshipRequestAndWaitForReceiptAsync(newMax);

            var maxTotal = await _fixture.SponsoredPaymasterService.MaxTotalDailySponsorshipQueryAsync();

            Assert.Equal(newMax, maxTotal);

            await _fixture.SponsoredPaymasterService.SetMaxTotalDailySponsorshipRequestAndWaitForReceiptAsync(
                Nethereum.Web3.Web3.Convert.ToWei(10));
        }

        [Fact]
        public async Task SetMaxDailySponsorPerUser_ByNonOwner_Fails()
        {
            var userService = _fixture.GetSponsoredPaymasterServiceForAccount(_fixture.UserAccounts[0]);

            var newMax = Nethereum.Web3.Web3.Convert.ToWei(5);

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => userService.SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(newMax));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task SetMaxTotalDailySponsorship_ByNonOwner_Fails()
        {
            var userService = _fixture.GetSponsoredPaymasterServiceForAccount(_fixture.UserAccounts[1]);

            var newMax = Nethereum.Web3.Web3.Convert.ToWei(50);

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => userService.SetMaxTotalDailySponsorshipRequestAndWaitForReceiptAsync(newMax));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task SetAccountRegistry_ByOwner_Updates()
        {
            var originalRegistry = await _fixture.SponsoredPaymasterService.AccountRegistryQueryAsync();

            var newRegistry = "0x1234567890123456789012345678901234567890";
            await _fixture.SponsoredPaymasterService.SetAccountRegistryRequestAndWaitForReceiptAsync(newRegistry);

            var currentRegistry = await _fixture.SponsoredPaymasterService.AccountRegistryQueryAsync();
            Assert.Equal(newRegistry.ToLower(), currentRegistry.ToLower());

            await _fixture.SponsoredPaymasterService.SetAccountRegistryRequestAndWaitForReceiptAsync(originalRegistry);
        }

        [Fact]
        public async Task SetAccountRegistry_ByNonOwner_Fails()
        {
            var userService = _fixture.GetSponsoredPaymasterServiceForAccount(_fixture.UserAccounts[2]);

            var newRegistry = "0x9999999999999999999999999999999999999999";

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => userService.SetAccountRegistryRequestAndWaitForReceiptAsync(newRegistry));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task TransferOwnership_ByNonOwner_Fails()
        {
            var userService = _fixture.GetSponsoredPaymasterServiceForAccount(_fixture.UserAccounts[3]);

            var newOwner = "0x1111111111111111111111111111111111111111";

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => userService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task DefaultAdminRole_ReturnsZeroBytes32()
        {
            var adminRole = await _fixture.SponsoredPaymasterService.DefaultAdminRoleQueryAsync();

            Assert.NotNull(adminRole);
            Assert.Equal(32, adminRole.Length);
            Assert.True(adminRole.All(b => b == 0));
        }

        [Fact]
        public async Task SponsorRole_ReturnsNonZeroBytes32()
        {
            var sponsorRole = await _fixture.SponsoredPaymasterService.SponsorRoleQueryAsync();

            Assert.NotNull(sponsorRole);
            Assert.Equal(32, sponsorRole.Length);
        }

        [Fact]
        public async Task HasRole_AdminRole_OwnerHasRole()
        {
            var adminRole = await _fixture.SponsoredPaymasterService.DefaultAdminRoleQueryAsync();

            var hasRole = await _fixture.SponsoredPaymasterService.HasRoleQueryAsync(
                adminRole, _fixture.OperatorAccount.Address);

            Assert.True(hasRole);
        }

        [Fact]
        public async Task HasRole_AdminRole_UserDoesNotHaveRole()
        {
            var adminRole = await _fixture.SponsoredPaymasterService.DefaultAdminRoleQueryAsync();
            var userAddress = _fixture.UserAccounts[0].Address;

            var hasRole = await _fixture.SponsoredPaymasterService.HasRoleQueryAsync(adminRole, userAddress);

            Assert.False(hasRole);
        }

        [Fact]
        public async Task GetRoleAdmin_ForDefaultAdminRole_ReturnsDefaultAdmin()
        {
            var adminRole = await _fixture.SponsoredPaymasterService.DefaultAdminRoleQueryAsync();

            var roleAdmin = await _fixture.SponsoredPaymasterService.GetRoleAdminQueryAsync(adminRole);

            Assert.NotNull(roleAdmin);
            Assert.Equal(adminRole, roleAdmin);
        }

        [Fact]
        public async Task SupportsInterface_ERC165_ReturnsTrue()
        {
            var erc165InterfaceId = new byte[] { 0x01, 0xff, 0xc9, 0xa7 };

            var supportsInterface = await _fixture.SponsoredPaymasterService.SupportsInterfaceQueryAsync(erc165InterfaceId);

            Assert.True(supportsInterface);
        }

        [Fact]
        public async Task SupportsInterface_IAccessControl_ReturnsTrue()
        {
            var accessControlInterfaceId = new byte[] { 0x79, 0x65, 0xdb, 0x0b };

            var supportsInterface = await _fixture.SponsoredPaymasterService.SupportsInterfaceQueryAsync(accessControlInterfaceId);

            Assert.True(supportsInterface);
        }

        [Fact]
        public async Task WithdrawTo_ByOwner_WithdrawsFunds()
        {
            var depositBefore = await _fixture.SponsoredPaymasterService.GetDepositQueryAsync();
            var withdrawAmount = Nethereum.Web3.Web3.Convert.ToWei(0.01m);
            var withdrawTo = "0x1111111111111111111111111111111111111111";

            var balanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(withdrawTo);
            await _fixture.SponsoredPaymasterService.WithdrawToRequestAndWaitForReceiptAsync(
                withdrawTo, withdrawAmount);
            var balanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(withdrawTo);

            Assert.Equal(withdrawAmount, balanceAfter.Value - balanceBefore.Value);

            var depositAfter = await _fixture.SponsoredPaymasterService.GetDepositQueryAsync();
            Assert.Equal(depositBefore - withdrawAmount, depositAfter);
        }

        [Fact]
        public async Task WithdrawTo_ByNonOwner_Fails()
        {
            var userService = _fixture.GetSponsoredPaymasterServiceForAccount(_fixture.UserAccounts[4]);

            var withdrawTo = _fixture.UserAccounts[4].Address;
            var withdrawAmount = Nethereum.Web3.Web3.Convert.ToWei(0.01m);

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => userService.WithdrawToRequestAndWaitForReceiptAsync(withdrawTo, withdrawAmount));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task Deposit_ByAnyone_IncreasesBalance()
        {
            var userService = _fixture.GetSponsoredPaymasterServiceForAccount(_fixture.UserAccounts[0]);

            var depositBefore = await _fixture.SponsoredPaymasterService.GetDepositQueryAsync();
            var depositAmount = Nethereum.Web3.Web3.Convert.ToWei(0.1m);

            await userService.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = depositAmount });

            var depositAfter = await _fixture.SponsoredPaymasterService.GetDepositQueryAsync();

            Assert.Equal(depositBefore + depositAmount, depositAfter);
        }
    }
}
