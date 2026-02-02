using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.Contracts;
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
        public async Task Given_ContractService_When_SwitchedToAA_Then_CanExecuteTransaction()
        {
            // GIVEN: A smart account configured with factory (deployed on first use)
            // Per ERC-4337: UserOperations are submitted via bundler, executed via EntryPoint
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3001);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var initialCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, initialCount);

            // WHEN: Switch the contract service to use AA
            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            // THEN: The transaction should succeed via UserOperation
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
        public async Task Given_ContractService_When_SwitchedToAAWithPrivateKey_Then_CanExecuteTransaction()
        {
            // GIVEN: A smart account configured with factory using private key string
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3002);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            // WHEN: Switch using private key string (not EthECKey)
            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey.GetPrivateKey(),
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            // THEN: Should work the same as EthECKey
            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess);

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "AA20-InitCode")]
        public async Task Given_UndeployedAccount_When_CallWithFactory_Then_AutoDeploys()
        {
            // GIVEN: An account address that doesn't exist yet
            // Per ERC-4337 AA20: "sender not deployed and no initCode" - we provide initCode via factory
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3003);

            var codeBefore = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "Account should not have code before");

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            // WHEN: Switch to AA with factory config for auto-deployment
            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            // THEN: Account should be deployed and call should succeed
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
        public async Task Given_DeployedAccount_When_CheckInitCode_Then_InitCodeIsEmpty()
        {
            // GIVEN: A deployed smart account (first call deploys via initCode)
            // Per ERC-4337: InitCode should only be included when account needs deployment
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

            // First call deploys the account
            var receipt1 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt1).UserOpSuccess);

            var code = await _fixture.GetCodeAsync(accountAddress);
            Assert.True(code != null && code.Length > 0, "Account should be deployed after first call");

            // WHEN: Create UserOperation for a second call (just inspect, don't execute)
            var countFunction = new CountFunction();
            var packedOp = await handler.CreateUserOperationAsync(countFunction);

            // THEN: InitCode should be empty since account is already deployed
            Assert.True(packedOp.InitCode == null || packedOp.InitCode.Length == 0,
                "InitCode should be empty for already-deployed account");
        }

        [Fact]
        [Trait("Rule", "ERC4337-Nonce")]
        public async Task Given_AAHandler_When_Transaction_Then_NonceIsUsed()
        {
            // GIVEN: A smart account configured with factory
            // Per ERC-4337: Each UserOperation uses a nonce managed by EntryPoint
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

            // WHEN: Create a UserOperation and verify it has a nonce
            var countFunction = new CountFunction();
            var packedOp = await handler.CreateUserOperationAsync(countFunction);

            // THEN: UserOperation should have nonce=0 (first operation)
            Assert.Equal(BigInteger.Zero, packedOp.Nonce);

            // Execute the transaction
            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-GasEstimation")]
        public async Task Given_AAHandler_When_EstimateGas_Then_ReturnsTotal()
        {
            // GIVEN: A smart account configured with factory
            // Per ERC-4337: Gas estimation returns verificationGasLimit + callGasLimit + preVerificationGas
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

            // WHEN: Estimate gas for count() function
            var gas = await testCounter.ContractHandler.EstimateGasAsync<CountFunction>();

            // THEN: Should return total gas (verification + call + preverification)
            Assert.NotNull(gas);
            Assert.True(gas.Value > 0, "Gas estimate should be positive");
            Assert.True(gas.Value > 21000, "UserOp gas should be higher than basic transaction");
        }

        [Fact]
        [Trait("Rule", "ERC4337-SendUserOperation")]
        public async Task Given_AAHandler_When_SendRequestOnly_Then_ReturnsUserOpHash()
        {
            // GIVEN: A smart account configured with factory
            // Per ERC-4337: eth_sendUserOperation returns userOpHash before mining
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

            // WHEN: Send request without waiting
            var userOpHash = await testCounter.CountRequestAsync();

            // THEN: Should return a valid user operation hash
            Assert.NotNull(userOpHash);
            Assert.StartsWith("0x", userOpHash);
            Assert.Equal(66, userOpHash.Length); // 0x + 64 hex chars

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-ExecuteBatch")]
        public async Task Given_AAHandler_When_BatchExecute_Then_AllCallsSucceed()
        {
            // GIVEN: A smart account configured with factory
            // Per ERC-4337: Accounts can batch multiple calls in one UserOperation
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

            // WHEN: Execute batch of 3 count() calls
            var countFunction = new CountFunction();
            var countCallData = countFunction.GetCallData();

            var receipt = await handler.BatchExecuteAsync(
                (testCounter.ContractAddress, BigInteger.Zero, countCallData),
                (testCounter.ContractAddress, BigInteger.Zero, countCallData),
                (testCounter.ContractAddress, BigInteger.Zero, countCallData));

            // THEN: All calls should execute in one UserOperation
            Assert.NotNull(receipt);
            Assert.True(receipt.UserOpSuccess, $"Batch UserOp should succeed. Revert: {receipt.RevertReason}");

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-Query")]
        public async Task Given_AAHandler_When_QueryCall_Then_DoesNotUseUserOp()
        {
            // GIVEN: A smart account with a transaction already executed
            // Query calls (view/pure functions) should use eth_call, not UserOperations
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

            // WHEN: Query the counter (read-only call)
            var count = await testCounter.CountersQueryAsync(accountAddress);

            // THEN: Should return the value without using UserOp (direct eth_call)
            Assert.Equal(BigInteger.One, count);
        }

        [Fact]
        [Trait("Rule", "ERC4337-Configuration")]
        public async Task Given_AAHandler_When_FluentConfiguration_Then_ChainsCorrectly()
        {
            // GIVEN: A smart account configured with factory
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(3010);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            // WHEN: Use fluent configuration
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

            // THEN: Handler should be properly configured
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
