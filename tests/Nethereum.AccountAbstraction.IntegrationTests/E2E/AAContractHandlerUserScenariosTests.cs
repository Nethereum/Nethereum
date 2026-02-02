using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    /// <summary>
    /// Comprehensive user scenario tests for AAContractHandler.
    /// These tests serve as both validation and reference documentation for using Account Abstraction.
    /// </summary>
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "AAContractHandler")]
    [Trait("Category", "UserScenarios")]
    [Trait("ERC", "4337")]
    public class AAContractHandlerUserScenariosTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public AAContractHandlerUserScenariosTests(DevChainBundlerFixture fixture)
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

        private async Task<(string tokenAddress, ERC20ContractService erc20Service)> DeployERC20TokenAsync(string name, string symbol, BigInteger initialSupply)
        {
            var tokenDeployment = new Nethereum.StandardTokenEIP20.ContractDefinition.EIP20Deployment
            {
                InitialAmount = initialSupply,
                TokenName = name,
                TokenSymbol = symbol,
                DecimalUnits = 18
            };

            var web3 = (Web3.Web3)_fixture.Web3;
            var receipt = await web3.Eth.GetContractDeploymentHandler<Nethereum.StandardTokenEIP20.ContractDefinition.EIP20Deployment>()
                .SendRequestAndWaitForReceiptAsync(tokenDeployment);

            var tokenAddress = receipt.ContractAddress;
            var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

            return (tokenAddress, erc20Service);
        }

        #region Scenario 1: ERC20 Token Operations via Account Abstraction

        [Fact]
        [Trait("Scenario", "ERC20-Transfer")]
        public async Task Scenario_ERC20Transfer_UsingERC20ContractService_WithAA()
        {
            // SCENARIO: User wants to transfer ERC20 tokens using a smart account
            // This demonstrates using the standard ERC20ContractService with AA

            // GIVEN: A smart account with tokens
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4001, 10m);

            // Deploy ERC20 token and get ERC20ContractService
            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "AA Test Token", "AAT", Web3.Web3.Convert.ToWei(1_000_000));

            // Fund the smart account with tokens using a standard transfer (deployer has all tokens)
            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(1000));

            var initialBalance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(1000), initialBalance);

            // WHEN: Switch to Account Abstraction and transfer tokens
            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var recipient = "0x" + new string('1', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(100);

            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, transferAmount);

            // THEN: Transfer should succeed via UserOperation
            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Transfer should succeed. Revert: {aaReceipt.RevertReason}");

            // Verify token balances
            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var senderBalance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(900), senderBalance);
        }

        [Fact]
        [Trait("Scenario", "ERC20-Approve-TransferFrom")]
        public async Task Scenario_ERC20ApproveAndTransferFrom_WithAA()
        {
            // SCENARIO: User approves a spender, then spender uses transferFrom
            // This demonstrates multiple AA operations with different accounts

            // GIVEN: Two smart accounts - owner and spender
            var (ownerAddress, ownerKey, ownerFactory) = await CreateAccountWithFactoryAsync(4002, 10m);
            var (spenderAddress, spenderKey, spenderFactory) = await CreateAccountWithFactoryAsync(4003, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Approve Test Token", "APT", Web3.Web3.Convert.ToWei(1_000_000));

            // Fund owner with tokens (deployer has all tokens)
            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                ownerAddress, Web3.Web3.Convert.ToWei(1000));

            var bundlerService = CreateBundlerAdapter();

            // WHEN: Owner approves spender using AA
            erc20Service.SwitchToAccountAbstraction(
                ownerAddress,
                ownerKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: ownerFactory);

            var approvalAmount = Web3.Web3.Convert.ToWei(500);
            var approveReceipt = await erc20Service.ApproveRequestAndWaitForReceiptAsync(
                spenderAddress, approvalAmount);

            Assert.True(((AATransactionReceipt)approveReceipt).UserOpSuccess, "Approval should succeed");

            // Verify allowance
            var allowance = await erc20Service.AllowanceQueryAsync(ownerAddress, spenderAddress);
            Assert.Equal(approvalAmount, allowance);

            // WHEN: Spender uses transferFrom via AA
            erc20Service.SwitchToAccountAbstraction(
                spenderAddress,
                spenderKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: spenderFactory);

            var recipient = "0x" + new string('2', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(200);

            var transferFromReceipt = await erc20Service.TransferFromRequestAndWaitForReceiptAsync(
                ownerAddress, recipient, transferAmount);

            // THEN: TransferFrom should succeed
            Assert.True(((AATransactionReceipt)transferFromReceipt).UserOpSuccess, "TransferFrom should succeed");

            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var newAllowance = await erc20Service.AllowanceQueryAsync(ownerAddress, spenderAddress);
            Assert.Equal(approvalAmount - transferAmount, newAllowance);
        }

        #endregion

        #region Scenario 2: Using web3.Eth.ERC20 Standard Service

        [Fact]
        [Trait("Scenario", "ERC20-StandardService")]
        public async Task Scenario_Web3EthERC20_SwitchToAccountAbstraction()
        {
            // SCENARIO: User wants to use web3.Eth.ERC20 with Account Abstraction
            // This demonstrates using the built-in ERC20 service with SwitchToAccountAbstraction

            // GIVEN: A smart account and deployed token
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4004, 10m);

            var (tokenAddress, _) = await DeployERC20TokenAsync(
                "Web3 ERC20 Token", "W3T", Web3.Web3.Convert.ToWei(1_000_000));

            // WHEN: Use web3.Eth.ERC20 and switch to AA
            var web3 = (Web3.Web3)_fixture.Web3;
            var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

            // Fund the smart account (deployer has all tokens)
            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(500));

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // Transfer tokens
            var recipient = "0x" + new string('3', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(50);

            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, transferAmount);

            // THEN: Transfer should succeed
            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);

            // Verify using query (still works, no AA needed for queries)
            var balance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, balance);
        }

        #endregion

        #region Scenario 3: Paymaster Integration

        [Fact]
        [Trait("Scenario", "Paymaster-Sponsorship")]
        public async Task Scenario_PaymasterSponsorship_WithAAHandler()
        {
            // SCENARIO: User wants gas sponsored by a paymaster
            // The smart account doesn't need ETH - paymaster pays for gas

            // GIVEN: A paymaster with deposit and a smart account WITHOUT ETH
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, 4005);

            // DON'T fund the account with ETH - paymaster will pay

            var factoryConfig = new FactoryConfig(
                _fixture.AccountFactoryService.ContractAddress,
                ownerAddress,
                4005);

            // Deploy token and send to account (no ETH, deployer has all tokens)
            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Paymaster Test Token", "PMT", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(100));

            // WHEN: Switch to AA with paymaster
            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig)
                .WithPaymaster(paymasterService.ContractAddress);

            // Transfer tokens (account has no ETH, paymaster sponsors)
            var recipient = "0x" + new string('4', 40);
            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, Web3.Web3.Convert.ToWei(10));

            // THEN: Transfer should succeed with paymaster sponsorship
            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Paymaster-sponsored transfer should succeed. Revert: {aaReceipt.RevertReason}");
            Assert.Equal(paymasterService.ContractAddress.ToLower(), aaReceipt.Paymaster?.ToLower());

            var balance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(Web3.Web3.Convert.ToWei(10), balance);
        }

        [Fact]
        [Trait("Scenario", "Paymaster-WithData")]
        public async Task Scenario_PaymasterWithStaticData_WithAAHandler()
        {
            // SCENARIO: Paymaster requires specific data for validation

            var paymasterService = await DeployAndFundPaymasterAsync(5m);
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4006, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            // WHEN: Configure with paymaster and static data
            var paymasterData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig)
                .WithPaymaster(paymasterService.ContractAddress, paymasterData);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            // THEN: Should succeed with paymaster data included
            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess);
            Assert.NotNull(aaReceipt.Paymaster);
        }

        #endregion

        #region Scenario 4: Batch Execution

        [Fact]
        [Trait("Scenario", "Batch-MultipleContracts")]
        public async Task Scenario_BatchExecution_MultipleContractCalls()
        {
            // SCENARIO: User wants to execute multiple contract calls atomically
            // All calls succeed or fail together in one UserOperation

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4007, 10m);

            // Deploy two different counters
            var counter1 = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());
            var counter2 = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = counter1.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Batch calls to different contracts
            var countCallData = new CountFunction().GetCallData();

            var receipt = await handler.BatchExecuteAsync(
                (counter1.ContractAddress, BigInteger.Zero, countCallData),
                (counter1.ContractAddress, BigInteger.Zero, countCallData),
                (counter2.ContractAddress, BigInteger.Zero, countCallData));

            // THEN: All calls should execute atomically
            Assert.True(receipt.UserOpSuccess, $"Batch should succeed. Revert: {receipt.RevertReason}");

            var count1 = await counter1.CountersQueryAsync(accountAddress);
            var count2 = await counter2.CountersQueryAsync(accountAddress);

            Assert.Equal(new BigInteger(2), count1); // Called twice
            Assert.Equal(BigInteger.One, count2);    // Called once
        }

        [Fact]
        [Trait("Scenario", "Batch-ERC20-MultiTransfer")]
        public async Task Scenario_BatchExecution_ERC20MultipleTransfers()
        {
            // SCENARIO: Transfer tokens to multiple recipients in one UserOperation

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4008, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Batch Token", "BAT", Web3.Web3.Convert.ToWei(1_000_000));

            // Fund smart account (deployer has all tokens)
            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(1000));

            var bundlerService = CreateBundlerAdapter();

            var handler = erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Batch multiple transfers
            var recipients = new[]
            {
                "0x" + new string('5', 40),
                "0x" + new string('6', 40),
                "0x" + new string('7', 40)
            };

            var transferAmount = Web3.Web3.Convert.ToWei(100);

            var calls = new (string target, BigInteger value, byte[] callData)[recipients.Length];
            for (int i = 0; i < recipients.Length; i++)
            {
                var transfer = new TransferFunction { To = recipients[i], Value = transferAmount };
                calls[i] = (tokenAddress, BigInteger.Zero, transfer.GetCallData());
            }

            var receipt = await handler.BatchExecuteAsync(calls);

            // THEN: All transfers should succeed
            Assert.True(receipt.UserOpSuccess);

            foreach (var recipient in recipients)
            {
                var balance = await erc20Service.BalanceOfQueryAsync(recipient);
                Assert.Equal(transferAmount, balance);
            }
        }

        #endregion

        #region Scenario 5: Sequential Operations and Nonce Management

        [Fact]
        [Trait("Scenario", "Sequential-Operations")]
        public async Task Scenario_SequentialOperations_NonceIncrements()
        {
            // SCENARIO: Multiple sequential operations using the same handler
            // Demonstrates that nonce is managed correctly

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4009, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Execute 5 sequential operations
            for (int i = 1; i <= 5; i++)
            {
                var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
                var aaReceipt = (AATransactionReceipt)receipt;
                Assert.True(aaReceipt.UserOpSuccess, $"Operation {i} should succeed");
            }

            // THEN: Counter should be 5
            var finalCount = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(5), finalCount);
        }

        #endregion

        #region Scenario 6: Receipt Inspection

        [Fact]
        [Trait("Scenario", "Receipt-Details")]
        public async Task Scenario_InspectAAReceipt_AllFieldsPopulated()
        {
            // SCENARIO: User wants to inspect all details of the AA receipt

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4010, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Execute a transaction
            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            // THEN: Inspect all AA receipt fields
            Assert.IsType<AATransactionReceipt>(receipt);
            var aaReceipt = (AATransactionReceipt)receipt;

            // Standard receipt fields
            Assert.NotNull(aaReceipt.TransactionHash);
            Assert.NotNull(aaReceipt.BlockNumber);
            Assert.NotNull(aaReceipt.BlockHash);
            Assert.True(aaReceipt.GasUsed.Value > 0);

            // AA-specific fields
            Assert.NotNull(aaReceipt.UserOpHash);
            Assert.Equal(66, aaReceipt.UserOpHash.Length); // 0x + 64 hex
            Assert.True(aaReceipt.UserOpSuccess);
            Assert.Null(aaReceipt.RevertReason); // No revert
            Assert.True(aaReceipt.ActualGasUsed > 0);
            Assert.True(aaReceipt.ActualGasCost > 0);
            Assert.Equal(accountAddress.ToLower(), aaReceipt.Sender?.ToLower());
        }

        #endregion

        #region Scenario 7: Configuration Variations

        [Fact]
        [Trait("Scenario", "Config-GasSettings")]
        public async Task Scenario_CustomGasConfig_AffectsPolling()
        {
            // SCENARIO: User wants custom gas config for receipt polling

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4011, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            // WHEN: Configure with custom gas settings
            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig)
                .WithGasConfig(new AAGasConfig
                {
                    ReceiptPollIntervalMs = 100,  // Fast polling
                    ReceiptTimeoutMs = 60000      // Long timeout
                });

            // THEN: Config should be applied
            Assert.Equal(100, handler.GasConfig.ReceiptPollIntervalMs);
            Assert.Equal(60000, handler.GasConfig.ReceiptTimeoutMs);

            // And operations should still work
            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);
        }

        [Fact]
        [Trait("Scenario", "Config-FullFluent")]
        public async Task Scenario_FluentConfiguration_AllOptions()
        {
            // SCENARIO: User wants to configure all options using fluent API

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4012, 10m);
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            // WHEN: Use full fluent configuration
            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress)
                .WithFactory(factoryConfig)
                .WithPaymaster(paymasterService.ContractAddress)
                .WithGasConfig(new AAGasConfig
                {
                    ReceiptPollIntervalMs = 500,
                    ReceiptTimeoutMs = 30000
                });

            // THEN: All configuration should be applied
            Assert.NotNull(handler.FactoryConfig);
            Assert.NotNull(handler.PaymasterConfig);
            Assert.Equal(500, handler.GasConfig.ReceiptPollIntervalMs);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);
        }

        #endregion

        #region Scenario 8: Query Operations (No AA)

        [Fact]
        [Trait("Scenario", "Query-NoUserOp")]
        public async Task Scenario_QueryOperations_DoNotUseUserOp()
        {
            // SCENARIO: Read-only queries should not create UserOperations
            // They should use normal eth_call

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4013, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Query Token", "QRY", Web3.Web3.Convert.ToWei(1_000_000));

            // Fund smart account (deployer has all tokens)
            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(100));

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Execute query operations
            var name = await erc20Service.NameQueryAsync();
            var symbol = await erc20Service.SymbolQueryAsync();
            var decimals = await erc20Service.DecimalsQueryAsync();
            var balance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            var totalSupply = await erc20Service.TotalSupplyQueryAsync();

            // THEN: All queries should work without UserOperations
            Assert.Equal("Query Token", name);
            Assert.Equal("QRY", symbol);
            Assert.Equal((byte)18, decimals);
            Assert.Equal(Web3.Web3.Convert.ToWei(100), balance);
            Assert.Equal(Web3.Web3.Convert.ToWei(1_000_000), totalSupply);
        }

        #endregion

        #region Scenario 9: CreateUserOperation for Inspection

        [Fact]
        [Trait("Scenario", "Inspect-UserOp")]
        public async Task Scenario_CreateUserOperationForInspection()
        {
            // SCENARIO: User wants to inspect the UserOperation before sending

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4014, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Create UserOperation without sending
            var countFunction = new CountFunction();
            var packedOp = await handler.CreateUserOperationAsync(countFunction);

            // THEN: Can inspect all UserOperation fields
            Assert.Equal(accountAddress.ToLower(), packedOp.Sender.ToLower());
            Assert.NotNull(packedOp.CallData);
            Assert.True(packedOp.CallData.Length > 0);
            Assert.NotNull(packedOp.Signature);
            Assert.True(packedOp.Signature.Length > 0);

            // InitCode should be present (account not deployed yet)
            Assert.NotNull(packedOp.InitCode);
            Assert.True(packedOp.InitCode.Length > 0);

            // Gas fields should be set
            Assert.True(packedOp.PreVerificationGas > 0);
        }

        #endregion

        #region Scenario 10: Error Handling

        [Fact]
        [Trait("Scenario", "Error-InnerRevert")]
        public async Task Scenario_InnerExecutionRevert_ReceiptShowsFailure()
        {
            // SCENARIO: Inner execution reverts, receipt should show failure with reason
            // This tests that revert reasons are captured in the AA receipt

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4015, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Revert Token", "REV", Web3.Web3.Convert.ToWei(1_000_000));

            // Account has NO tokens, so transfer should fail
            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Try to transfer more tokens than the account has
            var recipient = "0x" + new string('8', 40);
            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, Web3.Web3.Convert.ToWei(1000)); // Has 0, trying to send 1000

            // THEN: Receipt should indicate failure
            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.False(aaReceipt.UserOpSuccess, "Transfer with insufficient balance should fail");
            // Note: Revert reason may or may not be populated depending on how the token handles the error
        }

        #endregion

        #region Helper Methods

        private async Task<TestPaymasterAcceptAllService> DeployAndFundPaymasterAsync(decimal ethAmount)
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };

            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            var paymasterAddress = paymasterService.ContractAddress;

            // Add stake
            await paymasterService.AddStakeRequestAndWaitForReceiptAsync(
                new AddStakeFunction
                {
                    UnstakeDelaySec = 86400,
                    AmountToSend = Web3.Web3.Convert.ToWei(0.1m)
                });

            // Deposit to EntryPoint
            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = paymasterAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(ethAmount)
                });

            return paymasterService;
        }

        #endregion
    }
}
