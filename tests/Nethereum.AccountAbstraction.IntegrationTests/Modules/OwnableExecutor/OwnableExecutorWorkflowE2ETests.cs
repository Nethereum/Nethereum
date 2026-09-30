using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.ModularAccount;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Documentation;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.Modules.OwnableExecutor
{
    [Collection(ModularAccountBundlerCollection.COLLECTION_NAME)]
    [Trait("Category", "ERC7579-Module")]
    [Trait("Module", "OwnableExecutor")]
    [Trait("Workflow", "Delegation")]
    public class OwnableExecutorWorkflowE2ETests
    {
        private readonly ModularAccountBundlerFixture _fixture;

        public OwnableExecutorWorkflowE2ETests(ModularAccountBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var infra = new AADeploymentAddresses(
                _fixture.EntryPointService.ContractAddress,
                _fixture.FactoryService.ContractAddress,
                _fixture.EcdsaValidatorService.ContractAddress,
                VerifyingPaymasterAddress: string.Empty);

            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.Bootstrap.OperatorWeb3)
                .UseDeploymentAddresses(infra)
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        private async Task<(EthECKey key, IWeb3 web3)> CreateFundedEoaAsync()
        {
            var key = EthECKey.GenerateKey();
            await _fixture.Bootstrap.Node.SetBalanceAsync(key.GetPublicAddress(), Nethereum.Web3.Web3.Convert.ToWei(1));
            var web3 = new Nethereum.Web3.Web3(new Web3Account(key), _fixture.Bootstrap.OperatorWeb3.Client);
            return (key, web3);
        }

        private static byte[] BuildCountExecutionCallData(string counterAddress) =>
            ERC7579ExecutionLib.EncodeSingle(counterAddress, BigInteger.Zero, new CountFunction().GetCallData());

        [Fact]
        [Trait("UseCase", "OwnableExecutor")]
        [NethereumDocExample(DocSection.AccountAbstraction, "modular-accounts", "Install a module after deployment via the account's own self-call", Order = 10)]
        public async Task Given_a_funded_account_When_installing_OwnableExecutor_for_an_agent_Then_isModuleInstalled_becomes_true()
        {
            var client = BuildClient();
            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var executor = await OwnableExecutorService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new OwnableExecutorDeployment());
            var (agentKey, _) = await CreateFundedEoaAsync();

            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);

            var receipt = (AATransactionReceipt)await accountService.InstallOwnableExecutorAndWaitForReceiptAsync(
                executor.ContractAddress, agentKey.GetPublicAddress());

            Assert.True(receipt.UserOpSuccess, receipt.FailureDiagnostic);

            var moduleConfig = OwnableExecutorConfig.Create(executor.ContractAddress, agentKey.GetPublicAddress());
            Assert.True(await accountService.IsModuleInstalledAsync(moduleConfig));
        }

        [Fact]
        [Trait("UseCase", "OwnableExecutor")]
        public async Task Given_an_installed_executor_When_the_registered_agent_calls_executeOnOwnedAccount_Then_it_executes_on_the_account_with_a_plain_tx()
        {
            var client = BuildClient();
            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var executor = await OwnableExecutorService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new OwnableExecutorDeployment());
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());
            var (agentKey, agentWeb3) = await CreateFundedEoaAsync();

            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallOwnableExecutorAndWaitForReceiptAsync(
                executor.ContractAddress, agentKey.GetPublicAddress());
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var executorAsAgent = new OwnableExecutorService(agentWeb3, executor.ContractAddress);
            var executeReceipt = await executorAsAgent.ExecuteOnOwnedAccountRequestAndWaitForReceiptAsync(
                account.Address, BuildCountExecutionCallData(testCounter.ContractAddress));

            Assert.Equal((BigInteger)1, executeReceipt.Status.Value);

            var count = await testCounter.CountersQueryAsync(account.Address);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("UseCase", "OwnableExecutor")]
        public async Task Given_an_installed_executor_When_an_unregistered_EOA_calls_executeOnOwnedAccount_Then_it_reverts_and_the_account_is_untouched()
        {
            var client = BuildClient();
            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var executor = await OwnableExecutorService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new OwnableExecutorDeployment());
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());
            var (agentKey, _) = await CreateFundedEoaAsync();
            var (_, strangerWeb3) = await CreateFundedEoaAsync();

            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallOwnableExecutorAndWaitForReceiptAsync(
                executor.ContractAddress, agentKey.GetPublicAddress());
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var executorAsStranger = new OwnableExecutorService(strangerWeb3, executor.ContractAddress);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(() =>
                executorAsStranger.ExecuteOnOwnedAccountRequestAndWaitForReceiptAsync(
                    account.Address, BuildCountExecutionCallData(testCounter.ContractAddress)));
            Assert.True(ex.ExceptionEncodedData.IsExceptionEncodedDataForError<UnauthorizedAccessError>());

            var count = await testCounter.CountersQueryAsync(account.Address);
            Assert.Equal(BigInteger.Zero, count);
        }

        [Fact]
        [Trait("UseCase", "OwnableExecutor")]
        [NethereumDocExample(DocSection.AccountAbstraction, "modular-accounts", "Check and remove a module via IsModuleInstalledAsync/UninstallModuleAndWaitForReceiptAsync", Order = 11)]
        public async Task Given_an_installed_executor_When_uninstalling_it_Then_isModuleInstalled_becomes_false_and_the_agent_is_rejected()
        {
            var client = BuildClient();
            var owner = EthECKey.GenerateKey();
            var account = await client.CreateAccountAsync(owner);
            await _fixture.Bootstrap.Node.SetBalanceAsync(account.Address, Nethereum.Web3.Web3.Convert.ToWei(1));

            var executor = await OwnableExecutorService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new OwnableExecutorDeployment());
            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Bootstrap.OperatorWeb3, new TestCounterDeployment());
            var (agentKey, agentWeb3) = await CreateFundedEoaAsync();

            var accountService = new NethereumAccountService(_fixture.Bootstrap.OperatorWeb3, account.Address);
            accountService.UseAccountAbstraction(account, client);
            var installReceipt = (AATransactionReceipt)await accountService.InstallOwnableExecutorAndWaitForReceiptAsync(
                executor.ContractAddress, agentKey.GetPublicAddress());
            Assert.True(installReceipt.UserOpSuccess, installReceipt.FailureDiagnostic);

            var moduleConfig = OwnableExecutorConfig.Create(executor.ContractAddress, agentKey.GetPublicAddress());
            var uninstallReceipt = (AATransactionReceipt)await accountService.UninstallModuleAndWaitForReceiptAsync(moduleConfig);
            Assert.True(uninstallReceipt.UserOpSuccess, uninstallReceipt.FailureDiagnostic);

            Assert.False(await accountService.IsModuleInstalledAsync(moduleConfig));

            var executorAsAgent = new OwnableExecutorService(agentWeb3, executor.ContractAddress);
            await Assert.ThrowsAnyAsync<Exception>(() =>
                executorAsAgent.ExecuteOnOwnedAccountRequestAndWaitForReceiptAsync(
                    account.Address, BuildCountExecutionCallData(testCounter.ContractAddress)));
        }
    }
}
