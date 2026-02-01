using Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.DepositPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Paymasters
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class DepositPaymasterTests
    {
        private readonly BundlerTestFixture _fixture;

        public DepositPaymasterTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<DepositPaymasterService> DeployPaymasterAsync()
        {
            var deployment = new DepositPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };

            return await DepositPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);
        }

        [Fact]
        public async Task DeployPaymaster_WithValidParams_Succeeds()
        {
            var paymaster = await DeployPaymasterAsync();

            Assert.NotNull(paymaster);
            Assert.NotEmpty(paymaster.ContractAddress);
        }

        [Fact]
        public async Task GetEntryPoint_ReturnsConfiguredEntryPoint()
        {
            var paymaster = await DeployPaymasterAsync();

            var entryPoint = await paymaster.EntryPointQueryAsync();

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.ToLower());
        }

        [Fact]
        public async Task GetOwner_ReturnsConfiguredOwner()
        {
            var paymaster = await DeployPaymasterAsync();

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal(_fixture.BeneficiaryAddress.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task GetDeposit_PaymasterDeposit_InitiallyZero()
        {
            var paymaster = await DeployPaymasterAsync();

            var deposit = await paymaster.GetDepositQueryAsync();

            Assert.Equal(BigInteger.Zero, deposit);
        }

        [Fact]
        public async Task Deposits_UserDeposit_InitiallyZero()
        {
            var paymaster = await DeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var deposit = await paymaster.DepositsQueryAsync(accountAddress);

            Assert.Equal(BigInteger.Zero, deposit);
        }

        [Fact]
        public async Task Deposit_TransactionSucceeds()
        {
            var deployment = new DepositPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };

            var paymaster = await DepositPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var depositAmount = Web3.Web3.Convert.ToWei(0.1m);
            var depositFunction = new DepositFunction { AmountToSend = depositAmount };
            var receipt = await paymaster.DepositRequestAndWaitForReceiptAsync(depositFunction);

            Assert.NotNull(receipt);
            Assert.True(receipt.Succeeded());
        }

        [Fact]
        public async Task DepositFor_IncreasesUserBalance()
        {
            var paymaster = await DeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var depositAmount = Web3.Web3.Convert.ToWei(0.05m);
            var depositForFunction = new DepositForFunction
            {
                Account = accountAddress,
                AmountToSend = depositAmount
            };
            await paymaster.DepositForRequestAndWaitForReceiptAsync(depositForFunction);

            var userDeposit = await paymaster.DepositsQueryAsync(accountAddress);

            Assert.Equal(depositAmount, userDeposit);
        }

        [Fact]
        public async Task DepositFor_MultipleUsers_TracksIndependently()
        {
            var paymaster = await DeployPaymasterAsync();

            var salt1 = (ulong)Random.Shared.NextInt64();
            var salt2 = (ulong)Random.Shared.NextInt64();
            var (account1, _) = await _fixture.CreateFundedAccountAsync(salt1);
            var (account2, _) = await _fixture.CreateFundedAccountAsync(salt2);

            var deposit1 = Web3.Web3.Convert.ToWei(0.1m);
            var deposit2 = Web3.Web3.Convert.ToWei(0.2m);

            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = account1, AmountToSend = deposit1 });
            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = account2, AmountToSend = deposit2 });

            var balance1 = await paymaster.DepositsQueryAsync(account1);
            var balance2 = await paymaster.DepositsQueryAsync(account2);

            Assert.Equal(deposit1, balance1);
            Assert.Equal(deposit2, balance2);
        }

        [Fact]
        public async Task GetDepositInfo_ReturnsAccountInfo()
        {
            var paymaster = await DeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var depositAmount = Web3.Web3.Convert.ToWei(0.05m);
            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = accountAddress, AmountToSend = depositAmount });

            var info = await paymaster.GetDepositInfoQueryAsync(accountAddress);

            Assert.NotNull(info);
            Assert.Equal(depositAmount, info.Balance);
        }

        [Fact]
        public async Task MinDeposit_ReturnsConfiguredMinimum()
        {
            var paymaster = await DeployPaymasterAsync();

            var minDeposit = await paymaster.MinDepositQueryAsync();

            Assert.True(minDeposit >= BigInteger.Zero);
        }

        [Fact]
        public async Task SetMinDeposit_ByOwner_Updates()
        {
            var paymaster = await DeployPaymasterAsync();

            var newMinDeposit = Web3.Web3.Convert.ToWei(0.01m);
            await paymaster.SetMinDepositRequestAndWaitForReceiptAsync(newMinDeposit);

            var minDeposit = await paymaster.MinDepositQueryAsync();

            Assert.Equal(newMinDeposit, minDeposit);
        }

        [Fact]
        public async Task SetMinDeposit_ByNonOwner_Fails()
        {
            var paymaster = await DeployPaymasterAsync();

            var nonOwnerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var nonOwnerAddress = nonOwnerKey.GetPublicAddress();
            await _fixture.FundAccountAsync(nonOwnerAddress, 0.1m);

            var account = new Nethereum.Web3.Accounts.Account(TestAccounts.Account3PrivateKey);
            var nonOwnerWeb3 = new Web3.Web3(account, _fixture.Web3.Client);
            var nonOwnerPaymasterService = new DepositPaymasterService(nonOwnerWeb3, paymaster.ContractAddress);

            var newMinDeposit = Web3.Web3.Convert.ToWei(0.01m);

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonOwnerPaymasterService.SetMinDepositRequestAndWaitForReceiptAsync(newMinDeposit));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task Withdraw_ByUser_WithdrawsUserDeposit()
        {
            var paymaster = await DeployPaymasterAsync();

            var userKey = new EthECKey(TestAccounts.Account5PrivateKey);
            var userAddress = userKey.GetPublicAddress();
            await _fixture.FundAccountAsync(userAddress, 0.2m);

            var depositAmount = Web3.Web3.Convert.ToWei(0.1m);
            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = userAddress, AmountToSend = depositAmount });

            var account = new Nethereum.Web3.Accounts.Account(TestAccounts.Account5PrivateKey);
            var userWeb3 = new Web3.Web3(account, _fixture.Web3.Client);
            var userPaymasterService = new DepositPaymasterService(userWeb3, paymaster.ContractAddress);

            var withdrawAmount = Web3.Web3.Convert.ToWei(0.05m);
            await userPaymasterService.WithdrawRequestAndWaitForReceiptAsync(withdrawAmount);

            var remainingDeposit = await paymaster.DepositsQueryAsync(userAddress);
            var expectedRemaining = depositAmount - withdrawAmount;

            Assert.Equal(expectedRemaining, remainingDeposit);
        }

        [Fact]
        public async Task WithdrawTo_ByOwner_WithdrawsEntryPointDeposit()
        {
            var deployment = new DepositPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };

            var paymaster = await DepositPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var depositAmount = Web3.Web3.Convert.ToWei(0.1m);
            var depositFunction = new DepositFunction { AmountToSend = depositAmount };
            await paymaster.DepositRequestAndWaitForReceiptAsync(depositFunction);

            var withdrawTo = "0x1111111111111111111111111111111111111111";
            var withdrawAmount = Web3.Web3.Convert.ToWei(0.05m);

            var balanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(withdrawTo);
            await paymaster.WithdrawToRequestAndWaitForReceiptAsync(withdrawTo, withdrawAmount);
            var balanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(withdrawTo);

            Assert.Equal(withdrawAmount, balanceAfter.Value - balanceBefore.Value);
        }

        [Fact]
        public async Task TransferOwnership_ByOwner_TransfersOwnership()
        {
            var paymaster = await DeployPaymasterAsync();

            var newOwner = "0x1234567890123456789012345678901234567890";
            await paymaster.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner);

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal(newOwner.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task TransferOwnership_ByNonOwner_Fails()
        {
            var paymaster = await DeployPaymasterAsync();

            var nonOwnerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var nonOwnerAddress = nonOwnerKey.GetPublicAddress();
            await _fixture.FundAccountAsync(nonOwnerAddress, 0.1m);

            var account = new Nethereum.Web3.Accounts.Account(TestAccounts.Account4PrivateKey);
            var nonOwnerWeb3 = new Web3.Web3(account, _fixture.Web3.Client);
            var nonOwnerPaymasterService = new DepositPaymasterService(nonOwnerWeb3, paymaster.ContractAddress);

            var newOwner = "0x2222222222222222222222222222222222222222";

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonOwnerPaymasterService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task RenounceOwnership_ByOwner_RenounceOwnership()
        {
            var paymaster = await DeployPaymasterAsync();

            await paymaster.RenounceOwnershipRequestAndWaitForReceiptAsync();

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal("0x0000000000000000000000000000000000000000", owner.ToLower());
        }

        [Fact]
        public async Task DepositFor_MultipleDeposits_Accumulates()
        {
            var paymaster = await DeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var deposit1 = Web3.Web3.Convert.ToWei(0.03m);
            var deposit2 = Web3.Web3.Convert.ToWei(0.05m);
            var deposit3 = Web3.Web3.Convert.ToWei(0.02m);

            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = accountAddress, AmountToSend = deposit1 });
            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = accountAddress, AmountToSend = deposit2 });
            await paymaster.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = accountAddress, AmountToSend = deposit3 });

            var totalDeposit = await paymaster.DepositsQueryAsync(accountAddress);
            var expectedTotal = deposit1 + deposit2 + deposit3;

            Assert.Equal(expectedTotal, totalDeposit);
        }

        [Fact]
        public async Task MultiplePaymasters_IndependentUserDeposits()
        {
            var paymaster1 = await DeployPaymasterAsync();
            var paymaster2 = await DeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var deposit1 = Web3.Web3.Convert.ToWei(0.1m);
            var deposit2 = Web3.Web3.Convert.ToWei(0.2m);

            await paymaster1.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = accountAddress, AmountToSend = deposit1 });
            await paymaster2.DepositForRequestAndWaitForReceiptAsync(
                new DepositForFunction { Account = accountAddress, AmountToSend = deposit2 });

            var balance1 = await paymaster1.DepositsQueryAsync(accountAddress);
            var balance2 = await paymaster2.DepositsQueryAsync(accountAddress);

            Assert.Equal(deposit1, balance1);
            Assert.Equal(deposit2, balance2);
        }

        [Fact]
        public async Task GetDepositInfo_ForUnknownAccount_ReturnsZero()
        {
            var paymaster = await DeployPaymasterAsync();

            var unknownAccount = "0x9999999999999999999999999999999999999999";

            var info = await paymaster.GetDepositInfoQueryAsync(unknownAccount);

            Assert.NotNull(info);
            Assert.Equal(BigInteger.Zero, info.Balance);
        }
    }
}
