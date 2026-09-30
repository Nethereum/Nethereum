using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Documentation;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "AAContractHandler")]
    [Trait("ERC", "4337")]
    public class AAContractHandlerTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public AAContractHandlerTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private BundlerServiceAdapter CreateBundlerAdapter()
        {
            return new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID);
        }

        private async Task<(string accountAddress, EthECKey accountKey, FactoryConfig factoryConfig)> CreateAccountWithFactoryAsync(ulong salt, decimal ethAmount = 5m)
        {
            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, ethAmount);

            var factoryConfig = new FactoryConfig(
                _fixture.AccountFactoryService.ContractAddress,
                ownerAddress,
                salt);

            return (accountAddress, accountKey, factoryConfig);
        }

        [Fact]
        [Trait("Rule", "ERC4337-ContractHandler")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Switch contract service to AA with EthECKey", Order = 1)]
        public async Task Given_ContractService_When_SwitchedToAA_Then_CanExecuteTransaction()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3001);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var initialCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, initialCount);

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"UserOperation should succeed. Revert: {aaReceipt.RevertReason}");
            Assert.NotNull(aaReceipt.UserOpHash);
            Assert.Equal(accountAddress.ToLower(), aaReceipt.Sender?.ToLower());

            var finalCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, finalCount);
        }

        [Fact]
        [Trait("Rule", "ERC4337-ContractHandler")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Switch contract service to AA from a private key string (wrapped in EthECKey)", Order = 2)]
        public async Task Given_ContractService_When_SwitchedToAAWithPrivateKey_Then_CanExecuteTransaction()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3002);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                new EthECKey(accountKey.GetPrivateKey()),
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess);

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "AA20-InitCode")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Auto-deploy undeployed account with FactoryConfig", Order = 3)]
        public async Task Given_UndeployedAccount_When_CallWithFactory_Then_AutoDeploys()
        {
            // Per ERC-4337 AA20: "sender not deployed and no initCode" - we provide initCode via factory
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3003);

            var codeBefore = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "Account should not have code before");

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.NotNull(receipt);
            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"UserOp should succeed. Revert: {aaReceipt.RevertReason}");

            var codeAfter = await _fixture.GetCodeAsync(accountAddress);
            Assert.NotNull(codeAfter);
            Assert.True(codeAfter.Length > 0, "Account should have code after auto-deploy");

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "AA20-InitCode")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "InitCode is empty after account is deployed", Order = 4)]
        public async Task Given_DeployedAccount_When_CheckInitCode_Then_InitCodeIsEmpty()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3004);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt1 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt1).UserOpSuccess);

            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code != null && code.Length > 0, "Account should be deployed after first call");

            var countFunction = new CountFunction();
            var packedOp = await handler.CreateUserOperationAsync(countFunction);

            Assert.True(packedOp.InitCode == null || packedOp.InitCode.Length == 0,
                "InitCode should be empty for already-deployed account");
        }

        [Fact]
        [Trait("Rule", "ERC4337-Nonce")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Nonce management in AA handler", Order = 5)]
        public async Task Given_AAHandler_When_Transaction_Then_NonceIsUsed()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3005, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var countFunction = new CountFunction();
            var packedOp = await handler.CreateUserOperationAsync(countFunction);

            Assert.Equal(BigInteger.Zero, packedOp.Nonce);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-GasEstimation")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Gas estimation via AA handler", Order = 6)]
        public async Task Given_AAHandler_When_EstimateGas_Then_ReturnsTotal()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3006);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var gas = await testCounter.ContractHandler.EstimateGasAsync<CountFunction>();

            Assert.NotNull(gas);
            Assert.True(gas.Value > 0, "Gas estimate should be positive");
            Assert.True(gas.Value > 21000, "UserOp gas should be higher than basic transaction");
        }

        [Fact]
        [Trait("Rule", "ERC4337-SendUserOperation")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Send request without waiting returns UserOp hash", Order = 7)]
        public async Task Given_AAHandler_When_SendRequestOnly_Then_ReturnsUserOpHash()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3007);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var userOpHash = await testCounter.CountRequestAsync();

            Assert.NotNull(userOpHash);
            Assert.StartsWith("0x", userOpHash);
            Assert.Equal(66, userOpHash.Length);

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-ExecuteBatch")]
        [NethereumDocExample(DocSection.AccountAbstraction, "batching-and-paymasters", "Batch execute multiple calls in one UserOp", Order = 1)]
        public async Task Given_AAHandler_When_BatchExecute_Then_AllCallsSucceed()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3008, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var countCallData = new CountFunction().GetCallData();

            var receipt = await handler.BatchExecuteAsync(
                countCallData,
                countCallData,
                countCallData);

            Assert.NotNull(receipt);
            Assert.True(receipt.UserOpSuccess, $"Batch UserOp should succeed. Revert: {receipt.RevertReason}");

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-Query")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Query calls use eth_call not UserOp", Order = 8)]
        public async Task Given_AAHandler_When_QueryCall_Then_DoesNotUseUserOp()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3009);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            await testCounter.CountRequestAndWaitForReceiptAsync();

            var count = await testCounter.CountersQueryAsync(accountAddress);

            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-Configuration")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Fluent configuration with WithGasConfig", Order = 9)]
        public async Task Given_AAHandler_When_FluentConfiguration_Then_ChainsCorrectly()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3010);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig)
                .WithGasConfig(new AAGasConfig
                {
                    ReceiptPollIntervalMs = 500,
                    ReceiptTimeoutMs = 30000
                });

            Assert.NotNull(handler);
            Assert.Equal(accountAddress.ToLower(), handler.AccountAddress.ToLower());
            Assert.Equal(_fixture.EntryPointService.ContractAddress.ToLower(), handler.EntryPointAddress.ToLower());
            Assert.Equal(500, handler.GasConfig.ReceiptPollIntervalMs);
            Assert.Equal(30000, handler.GasConfig.ReceiptTimeoutMs);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);
        }
    }
}
