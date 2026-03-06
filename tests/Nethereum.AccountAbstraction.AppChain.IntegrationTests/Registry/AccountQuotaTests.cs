using Nethereum.AccountAbstraction.AppChain.Contracts.Policy.AccountRegistry;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.Fixtures;
using Nethereum.AccountAbstraction.AppChain.Interfaces;
using Nethereum.Contracts;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.Registry
{
    [Collection(AAIntegrationFixture.COLLECTION_NAME)]
    public class AccountQuotaTests
    {
        private readonly AAIntegrationFixture _fixture;

        public AccountQuotaTests(AAIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SetQuota_ByAdmin_Succeeds()
        {
            var account = "0xdddddddddddddddddddddddddddddddddddddddd";
            var gasQuota = BigInteger.Parse("10000000000000000000");
            uint opQuota = 1000;
            var valueQuota = BigInteger.Parse("5000000000000000000");

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(account);

            await _fixture.AccountRegistryService.SetQuotaRequestAndWaitForReceiptAsync(
                account, gasQuota, opQuota, valueQuota);

            var accountInfo = await _fixture.AccountRegistryService.GetAccountInfoQueryAsync(account);
            Assert.Equal(gasQuota, accountInfo.ReturnValue1.DailyGasQuota);
            Assert.Equal(opQuota, accountInfo.ReturnValue1.DailyOpQuota);
            Assert.Equal(valueQuota, accountInfo.ReturnValue1.DailyValueQuota);
        }

        [Fact]
        public async Task SetQuota_ByNonAdmin_Fails()
        {
            var nonAdmin = _fixture.UserAccounts[0];
            var targetAccount = "0xeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            var gasQuota = BigInteger.Parse("10000000000000000000");

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(targetAccount);

            var nonAdminService = _fixture.GetAccountRegistryServiceForAccount(nonAdmin);

            await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonAdminService.SetQuotaRequestAndWaitForReceiptAsync(
                    targetAccount, gasQuota, 100, gasQuota));
        }

        [Fact]
        public async Task GetRemainingQuota_ReturnsFullQuota_WhenNoneUsed()
        {
            var account = "0xfffffffffffffffffffffffffffffffffffffffA";
            var gasQuota = BigInteger.Parse("10000000000000000000");
            uint opQuota = 1000;
            var valueQuota = BigInteger.Parse("5000000000000000000");

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetQuotaRequestAndWaitForReceiptAsync(
                account, gasQuota, opQuota, valueQuota);

            var remaining = await _fixture.AccountRegistryService.GetRemainingQuotaQueryAsync(account);

            Assert.Equal(gasQuota, remaining.RemainingGas);
            Assert.Equal(opQuota, remaining.RemainingOps);
            Assert.Equal(valueQuota, remaining.RemainingValue);
        }

        [Fact]
        public async Task CheckQuota_ReturnsAllowed_ForValidUsage()
        {
            var account = _fixture.OperatorAccount.Address;
            var gasEstimate = BigInteger.Parse("100000");
            var valueEstimate = BigInteger.Parse("1000000000000000");

            var result = await _fixture.AccountRegistryService.CheckQuotaQueryAsync(
                account, gasEstimate, valueEstimate);

            Assert.True(result.Allowed, $"Check should be allowed: {result.Reason}");
        }

        [Fact]
        public async Task CheckQuota_ReturnsNotAllowed_ForInactiveAccount()
        {
            var unknownAccount = "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFb";
            var gasEstimate = BigInteger.Parse("100000");
            var valueEstimate = BigInteger.Parse("1000000000000000");

            var result = await _fixture.AccountRegistryService.CheckQuotaQueryAsync(
                unknownAccount, gasEstimate, valueEstimate);

            Assert.False(result.Allowed, "Check should not be allowed for inactive account");
        }

        [Fact]
        public async Task UseQuota_DeductsFromRemaining()
        {
            var account = "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFc";
            var gasQuota = BigInteger.Parse("10000000000000000000");
            uint opQuota = 1000;
            var valueQuota = BigInteger.Parse("5000000000000000000");

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetQuotaRequestAndWaitForReceiptAsync(
                account, gasQuota, opQuota, valueQuota);

            var gasUsed = BigInteger.Parse("1000000000000000000");
            var valueUsed = BigInteger.Parse("500000000000000000");

            await _fixture.AccountRegistryService.UseQuotaRequestAndWaitForReceiptAsync(
                account, gasUsed, valueUsed);

            var remaining = await _fixture.AccountRegistryService.GetRemainingQuotaQueryAsync(account);
            Assert.Equal(gasQuota - gasUsed, remaining.RemainingGas);
            Assert.Equal(valueQuota - valueUsed, remaining.RemainingValue);
        }

        [Fact]
        public async Task DefaultQuota_QueriesReturnDefaults()
        {
            var defaultGas = await _fixture.AccountRegistryService.DefaultGasQuotaQueryAsync();
            var defaultOps = await _fixture.AccountRegistryService.DefaultOpQuotaQueryAsync();
            var defaultValue = await _fixture.AccountRegistryService.DefaultValueQuotaQueryAsync();

            Assert.True(defaultGas > 0, "Default gas quota should be greater than 0");
            Assert.True(defaultOps > 0, "Default op quota should be greater than 0");
            Assert.True(defaultValue > 0, "Default value quota should be greater than 0");
        }

        [Fact]
        public async Task SetDefaultQuotas_ByAdmin_Succeeds()
        {
            var newGasQuota = BigInteger.Parse("20000000000000000000");
            uint newOpQuota = 2000;
            var newValueQuota = BigInteger.Parse("10000000000000000000");

            await _fixture.AccountRegistryService.SetDefaultQuotasRequestAndWaitForReceiptAsync(
                newGasQuota, newOpQuota, newValueQuota);

            var currentGas = await _fixture.AccountRegistryService.DefaultGasQuotaQueryAsync();
            var currentOps = await _fixture.AccountRegistryService.DefaultOpQuotaQueryAsync();
            var currentValue = await _fixture.AccountRegistryService.DefaultValueQuotaQueryAsync();

            Assert.Equal(newGasQuota, currentGas);
            Assert.Equal(newOpQuota, currentOps);
            Assert.Equal(newValueQuota, currentValue);
        }

        [Fact]
        public async Task ResetQuota_ByAdmin_ResetsUsageToZero()
        {
            var account = "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFd";
            var gasQuota = BigInteger.Parse("10000000000000000000");
            uint opQuota = 1000;
            var valueQuota = BigInteger.Parse("5000000000000000000");

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetQuotaRequestAndWaitForReceiptAsync(
                account, gasQuota, opQuota, valueQuota);

            await _fixture.AccountRegistryService.UseQuotaRequestAndWaitForReceiptAsync(
                account, BigInteger.Parse("5000000000000000000"), BigInteger.Parse("2000000000000000000"));

            var remainingBefore = await _fixture.AccountRegistryService.GetRemainingQuotaQueryAsync(account);
            Assert.True(remainingBefore.RemainingGas < gasQuota, "Gas should be partially used");

            await _fixture.AccountRegistryService.ResetQuotaRequestAndWaitForReceiptAsync(account);

            var remainingAfter = await _fixture.AccountRegistryService.GetRemainingQuotaQueryAsync(account);
            Assert.Equal(gasQuota, remainingAfter.RemainingGas);
            Assert.Equal(opQuota, remainingAfter.RemainingOps);
            Assert.Equal(valueQuota, remainingAfter.RemainingValue);
        }

        [Fact]
        public async Task AccountInfo_TracksUsageStatistics()
        {
            var account = "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFe";
            var gasQuota = BigInteger.Parse("10000000000000000000");
            uint opQuota = 1000;
            var valueQuota = BigInteger.Parse("5000000000000000000");

            await _fixture.AccountRegistryService.InviteRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetSelfActivationEnabledRequestAndWaitForReceiptAsync(true);
            await _fixture.AccountRegistryService.ActivateAccountRequestAndWaitForReceiptAsync(account);
            await _fixture.AccountRegistryService.SetQuotaRequestAndWaitForReceiptAsync(
                account, gasQuota, opQuota, valueQuota);

            var gasUsed = BigInteger.Parse("1000000000000000000");
            var valueUsed = BigInteger.Parse("500000000000000000");

            await _fixture.AccountRegistryService.UseQuotaRequestAndWaitForReceiptAsync(
                account, gasUsed, valueUsed);

            var accountInfo = await _fixture.AccountRegistryService.GetAccountInfoQueryAsync(account);
            Assert.Equal(gasUsed, accountInfo.ReturnValue1.GasUsedToday);
            Assert.Equal(valueUsed, accountInfo.ReturnValue1.ValueUsedToday);
            Assert.True(accountInfo.ReturnValue1.TotalGasUsed >= gasUsed, "Total gas should include current usage");
            Assert.True(accountInfo.ReturnValue1.TotalValue >= valueUsed, "Total value should include current usage");
        }

        [Fact]
        public async Task DefaultDailyQuota_QueriesReturnDefaults()
        {
            var dailyGas = await _fixture.AccountRegistryService.DefaultDailyGasQuotaQueryAsync();
            var dailyOps = await _fixture.AccountRegistryService.DefaultDailyOpQuotaQueryAsync();
            var dailyValue = await _fixture.AccountRegistryService.DefaultDailyValueQuotaQueryAsync();

            Assert.True(dailyGas >= 0, "Daily gas quota should be non-negative");
            Assert.True(dailyOps >= 0, "Daily op quota should be non-negative");
            Assert.True(dailyValue >= 0, "Daily value quota should be non-negative");
        }
    }
}
