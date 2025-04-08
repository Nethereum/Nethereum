using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Nethereum.Signer;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;

namespace Nethereum.AccountAbstraction.IntegrationTests
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class StakeManagementTests
    {
        private readonly EthereumClientIntegrationFixture _fixture;
        private readonly Web3.Web3 _web3;
        private readonly string _addr;
        private readonly EthECKey _ethKey;
        private EntryPointService _entryPoint;

        public StakeManagementTests(EthereumClientIntegrationFixture fixture)
        {
            _fixture = fixture;
            _web3 = _fixture.GetWeb3();
            _addr = EthereumClientIntegrationFixture.AccountAddress;
            _ethKey = new EthECKey(EthereumClientIntegrationFixture.AccountPrivateKey);
        }

        private async Task<EntryPointService> DeployEntryPointAsync()
        {
            return await EntryPointService.DeployContractAndGetServiceAsync(_web3, new EntryPointDeployment());
        }

        [Fact]
        public async Task ShouldDepositEtherToEntryPoint()
        {
            _entryPoint = await DeployEntryPointAsync();

            var sender2 = _web3.TransactionManager.Account;
            var fundTx = await _web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(_entryPoint.ContractAddress, 1.0m);

            var balance = await _entryPoint.BalanceOfQueryAsync(_addr);
            Assert.True(balance > 0);

            var depositInfoOutput = await _entryPoint.GetDepositInfoQueryAsync(_addr);
            var depositInfo = depositInfoOutput.Info;

            Assert.False(depositInfo.Staked);
            Assert.Equal(0, depositInfo.Stake);
            Assert.Equal((uint)0, depositInfo.UnstakeDelaySec);
            Assert.Equal((uint)0, depositInfo.WithdrawTime);
        }

        [Fact]
        public async Task ShouldFailStakeWithoutValue()
        {
            _entryPoint = await DeployEntryPointAsync();

            var ex = await Assert.ThrowsAsync<SmartContractRevertException>(() =>
                _entryPoint.AddStakeRequestAndWaitForReceiptAsync(2));

            Assert.Contains("no stake specified", ex.Message);
        }

        [Fact]
        public async Task ShouldFailStakeWithoutDelay()
        {
            _entryPoint = await DeployEntryPointAsync();

            var txn = new AddStakeFunction() { UnstakeDelaySec = 0, AmountToSend = Web3.Web3.Convert.ToWei(1) };

            var ex = await Assert.ThrowsAsync<SmartContractRevertException>(() =>
                _entryPoint.AddStakeRequestAndWaitForReceiptAsync(txn));

            Assert.Contains("must specify unstake delay", ex.Message);
        }

        [Fact]
        public async Task ShouldStakeAndReStake()
        {
            _entryPoint = await DeployEntryPointAsync();

            // Initial stake 2 ETH
            await _entryPoint.AddStakeRequestAndWaitForReceiptAsync(new AddStakeFunction
            {
                UnstakeDelaySec = 2,
                AmountToSend = Web3.Web3.Convert.ToWei(2)
            });

            var depositInfoOutput = await _entryPoint.GetDepositInfoQueryAsync(_addr);
            var depositInfo = depositInfoOutput.Info;
           Assert.True(depositInfo.Staked);
            Assert.Equal(Web3.Web3.Convert.ToWei(2), depositInfo.Stake);
            Assert.Equal((uint)0, depositInfo.WithdrawTime);

            // Add another 1 ETH
            await _entryPoint.AddStakeRequestAndWaitForReceiptAsync(new AddStakeFunction
            {
                UnstakeDelaySec = 2,
                AmountToSend = Web3.Web3.Convert.ToWei(1)
            });

            var updatedInfoOutput = await _entryPoint.GetDepositInfoQueryAsync(_addr);
            var updatedInfo = updatedInfoOutput.Info;
            Assert.Equal(Web3.Web3.Convert.ToWei(3), updatedInfo.Stake);
        }

 

        [Fact]
        public async Task ShouldDepositAndWithdrawFromSimpleAccount()
        {
            _entryPoint = await DeployEntryPointAsync();

            var factory = await SimpleAccountFactoryService.DeployContractAndGetServiceAsync(_web3,
                new SimpleAccountFactoryDeployment { EntryPoint = _entryPoint.ContractAddress });

            var result = await factory.CreateAndDeployAccountAsync(
                _addr, _addr, _entryPoint.ContractAddress, _ethKey, 0.01m, 123);

           

            var account = new SimpleAccountService(_web3, result.AccountAddress);
            
            var depositAfterDeployment = await account.GetDepositQueryAsync();
            await account.AddDepositRequestAndWaitForReceiptAsync(new AddDepositFunction
            {
                AmountToSend = Web3.Web3.Convert.ToWei(1)
            });

            var deposit = await account.GetDepositQueryAsync();
            Assert.Equal(Web3.Web3.Convert.ToWei(1) + depositAfterDeployment, deposit);

            await account.WithdrawDepositToRequestAndWaitForReceiptAsync(_addr, deposit);
            var depositAfter = await account.GetDepositQueryAsync();
            Assert.Equal(0, depositAfter);
        }

        [Fact]
        public async Task ShouldUnlockAndWithdrawStake()
        {
            _entryPoint = await DeployEntryPointAsync();

            // Stake 2 ETH
            await _entryPoint.AddStakeRequestAndWaitForReceiptAsync(new AddStakeFunction
            {
                UnstakeDelaySec = 1,
                AmountToSend = Web3.Web3.Convert.ToWei(2)
            });

            await _entryPoint.UnlockStakeRequestAndWaitForReceiptAsync();

            var unlockInfoOutput = await _entryPoint.GetDepositInfoQueryAsync(_addr);
            var unlockInfo = unlockInfoOutput.Info;

            Assert.False(unlockInfo.Staked);

            await Task.Delay(2000); // Wait for unlock delay
            var fundTx = await _web3.Eth.GetEtherTransferService()
               .TransferEtherAndWaitForReceiptAsync(_addr, 0.01m); // Dummy transaction to ensure the unlock is processed

            // Attempt withdrawal
            var recipient = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
            var receipt = await _entryPoint.WithdrawStakeRequestAndWaitForReceiptAsync(recipient);

            var afterWithdraw = await _entryPoint.GetDepositInfoQueryAsync(_addr);
            Assert.Equal(0, afterWithdraw.Info.Stake);
        }
    }

}
