using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures;
using Nethereum.AccountAbstraction.AppChain.Interfaces;
using Nethereum.Contracts;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Registry
{
    [Collection(AAIntegrationFixture.COLLECTION_NAME)]
    public class AccountStatusLifecycleTests
    {
        private readonly AAIntegrationFixture _fixture;

        public AccountStatusLifecycleTests(AAIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void AccountStatus_HasExpectedEnumValues()
        {
            Assert.Equal(0, (int)AccountStatus.None);
            Assert.Equal(1, (int)AccountStatus.Invited);
            Assert.Equal(2, (int)AccountStatus.Active);
        }

        [Fact]
        public async Task NewAccount_HasUnknownStatus()
        {
            var newAccountAddress = _fixture.UserAccounts[0].Address;

            var status = await _fixture.AccountRegistryService.GetStatusQueryAsync(newAccountAddress);

            Assert.Equal((byte)AccountStatus.None, status);
        }

        [Fact]
        public async Task InviteAccount_TransitionsToInvitedStatus()
        {
            var accountToInvite = _fixture.UserAccounts[1].Address;

            var statusBefore = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToInvite);
            Assert.Equal((byte)AccountStatus.None, statusBefore);

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToInvite);

            var statusAfter = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToInvite);
            Assert.Equal((byte)AccountStatus.Invited, statusAfter);
        }

        [Fact]
        public async Task ActivateAccount_RequiresPreviousInvitation()
        {
            var uninvitedAccount = _fixture.UserAccounts[2].Address;

            var uninvitedService = _fixture.GetAccountRegistryServiceForAccount(_fixture.UserAccounts[2]);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => uninvitedService.ActivateRequestAndWaitForReceiptAsync());
        }

        [Fact]
        public async Task ActivateAccount_TransitionsFromInvitedToActive()
        {
            var accountToActivate = _fixture.UserAccounts[3].Address;

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToActivate);

            var statusBefore = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToActivate);
            Assert.Equal((byte)AccountStatus.Invited, statusBefore);

            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);

            var userService = _fixture.GetAccountRegistryServiceForAccount(_fixture.UserAccounts[3]);
            await userService.ActivateRequestAndWaitForReceiptAsync();

            var statusAfter = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToActivate);
            Assert.Equal((byte)AccountStatus.Active, statusAfter);
        }

        [Fact]
        public async Task BanAccount_TransitionsFromActiveToBlocked()
        {
            var accountToBan = _fixture.UserAccounts[4].Address;

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToBan);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);

            var userService = _fixture.GetAccountRegistryServiceForAccount(_fixture.UserAccounts[4]);
            await userService.ActivateRequestAndWaitForReceiptAsync();

            var statusBefore = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToBan);
            Assert.Equal((byte)AccountStatus.Active, statusBefore);

            await _fixture.AccountRegistryService.BanRequestAndWaitForReceiptAsync(accountToBan, "Test ban reason");

            var statusAfter = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToBan);
            Assert.Equal((byte)AccountStatus.Banned, statusAfter);
        }

        [Fact]
        public async Task UnbanAccount_TransitionsFromBannedToActive()
        {
            var accountToUnban = _fixture.BundlerAccount.Address;

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToUnban);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);

            var bundlerService = _fixture.GetAccountRegistryServiceForAccount(_fixture.BundlerAccount);
            await bundlerService.ActivateRequestAndWaitForReceiptAsync();

            await _fixture.AccountRegistryService.BanRequestAndWaitForReceiptAsync(accountToUnban, "Test ban");

            var statusBanned = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToUnban);
            Assert.Equal((byte)AccountStatus.Banned, statusBanned);

            await _fixture.AccountRegistryService.UnbanRequestAndWaitForReceiptAsync(accountToUnban);

            var statusUnbanned = await _fixture.AccountRegistryService.GetStatusQueryAsync(accountToUnban);
            Assert.Equal((byte)AccountStatus.Active, statusUnbanned);
        }

        [Fact]
        public async Task IsActive_ReturnsTrueForActiveAccounts()
        {
            var activeAccount = _fixture.OperatorAccount.Address;

            var isActive = await _fixture.AccountRegistryService.IsActiveQueryAsync(activeAccount);

            Assert.True(isActive, "Operator (admin) should be active");
        }

        [Fact]
        public async Task IsActive_ReturnsFalseForNonActiveAccounts()
        {
            var unknownAccount = "0x1111111111111111111111111111111111111111";

            var isActive = await _fixture.AccountRegistryService.IsActiveQueryAsync(unknownAccount);

            Assert.False(isActive, "Unknown account should not be active");
        }

        [Fact]
        public async Task GetAccountInfo_ReturnsCompleteData()
        {
            var account = _fixture.OperatorAccount.Address;

            var info = await _fixture.AccountRegistryService.GetAccountInfoQueryAsync(account);

            Assert.NotNull(info);
            Assert.NotNull(info.ReturnValue1);
            Assert.Equal((byte)AccountStatus.Active, info.ReturnValue1.Status);
        }

        [Fact]
        public async Task GetAccountInfo_IncludesInviterAddress()
        {
            var inviter = _fixture.OperatorAccount.Address;
            var invitee = "0x2222222222222222222222222222222222222222";

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(invitee);

            var info = await _fixture.AccountRegistryService.GetAccountInfoQueryAsync(invitee);

            Assert.NotNull(info.ReturnValue1);
            Assert.Equal(inviter.ToLower(), info.ReturnValue1.InvitedBy.ToLower());
        }

        [Fact]
        public async Task InviteAccount_ByActiveMember_Succeeds()
        {
            var inviteeAddress = "0x3333333333333333333333333333333333333333";

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(inviteeAddress);

            var status = await _fixture.AccountRegistryService.GetStatusQueryAsync(inviteeAddress);
            Assert.Equal((byte)AccountStatus.Invited, status);
        }

        [Fact]
        public async Task BanAccount_ByNonAdmin_Fails()
        {
            var nonAdminAccount = _fixture.UserAccounts[0];
            var accountToBan = "0x4444444444444444444444444444444444444444";

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToBan);

            var nonAdminService = _fixture.GetAccountRegistryServiceForAccount(nonAdminAccount);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonAdminService.BanRequestAndWaitForReceiptAsync(accountToBan, "Unauthorized ban attempt"));
        }

        [Fact]
        public async Task GetAccountCount_ReturnsCorrectCount()
        {
            var initialCount = await _fixture.AccountRegistryService.GetAccountCountQueryAsync();
            Assert.True(initialCount >= 1, "Should have at least the admin account");
        }

        [Fact]
        public async Task InviteBatch_InvitesMultipleAccounts()
        {
            var accounts = new List<string>
            {
                "0x5555555555555555555555555555555555555555",
                "0x6666666666666666666666666666666666666666",
                "0x7777777777777777777777777777777777777777"
            };

            await _fixture.AccountRegistryService.InviteBatchRequestAndWaitForReceiptAsync(accounts);

            foreach (var account in accounts)
            {
                var status = await _fixture.AccountRegistryService.GetStatusQueryAsync(account);
                Assert.Equal((byte)AccountStatus.Invited, status);
            }
        }

        [Fact]
        public async Task SelfActivation_WhenDisabled_Fails()
        {
            var accountToActivate = "0x8888888888888888888888888888888888888888";

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(accountToActivate);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(false);

            var keyBytes = new byte[32];
            keyBytes[31] = 0x88;
            var privateKey = "0x" + BitConverter.ToString(keyBytes).Replace("-", "").ToLowerInvariant();
            var testAccount = new Nethereum.Web3.Accounts.Account(privateKey, 1);

            await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(testAccount.Address, 0.1m);

            var testService = new AccountRegistryService(
                _fixture.GetWeb3ForAccount(testAccount),
                _fixture.AccountRegistryService.ContractAddress);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => testService.ActivateRequestAndWaitForReceiptAsync());
        }
    }
}
