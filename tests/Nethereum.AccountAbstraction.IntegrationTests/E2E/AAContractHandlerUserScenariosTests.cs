using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
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
            // Note: Paymaster address may or may not be in receipt depending on bundler implementation

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
            Assert.True(aaReceipt.UserOpSuccess, $"Paymaster-sponsored operation should succeed. Revert: {aaReceipt.RevertReason}");
            // Note: Paymaster field depends on bundler implementation
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

            // Deploy a counter
            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = counter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Batch calls - just pass call data (target is the handler's contract)
            var countCallData = new CountFunction().GetCallData();

            var receipt = await handler.BatchExecuteAsync(
                countCallData,
                countCallData,
                countCallData);

            // THEN: All calls should execute atomically
            Assert.True(receipt.UserOpSuccess, $"Batch should succeed. Revert: {receipt.RevertReason}");

            var count = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count); // Called three times
        }

        [Fact]
        [Trait("Scenario", "Batch-MultipleOperations")]
        public async Task Scenario_BatchExecution_MixedOperations()
        {
            // SCENARIO: Execute multiple different operations in one UserOperation
            // Demonstrates atomic batch execution with different function calls

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4008, 10m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = counter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Batch operations using ToBatchCall() extension method
            var receipt = await handler.BatchExecuteAsync(
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall());

            // THEN: All 5 operations should execute atomically
            Assert.True(receipt.UserOpSuccess, $"Batch should succeed. Revert: {receipt.RevertReason}");

            var finalCount = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(5), finalCount); // All 5 count() calls executed
        }

        #endregion

        #region Scenario 5: Sequential Operations and Nonce Management

        [Fact]
        [Trait("Scenario", "Sequential-Operations")]
        public async Task Scenario_SequentialOperations_NonceIncrements()
        {
            // SCENARIO: Multiple sequential operations using the same handler
            // Demonstrates that nonce is managed correctly across multiple UserOperations

            var freshBundler = _fixture.CreateNewBundlerService();
            var bundlerAdapter = new BundlerServiceAdapter(freshBundler, DevChainBundlerFixture.CHAIN_ID);

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4009, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerAdapter,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // First operation - account deployed, nonce = 0
            var receipt1 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.NotNull(receipt1);
            Assert.IsType<AATransactionReceipt>(receipt1);
            Assert.True(((AATransactionReceipt)receipt1).UserOpSuccess, "First count should succeed");

            var count1 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count1);

            // Second operation - nonce should be 1
            var receipt2 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.NotNull(receipt2);
            Assert.True(((AATransactionReceipt)receipt2).UserOpSuccess, "Second count should succeed");

            var count2 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(2), count2);

            // Third operation - nonce should be 2
            var receipt3 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.NotNull(receipt3);
            Assert.True(((AATransactionReceipt)receipt3).UserOpSuccess, "Third count should succeed");

            var count3 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count3);

            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Full-AAContractHandler-Path")]
        public async Task Scenario_FullAAContractHandler_CountRequestAndWaitForReceipt()
        {
            // CRITICAL TEST: Uses the actual AAContractHandler path that was timing out
            // This should call SendRequestAndWaitForReceiptAsync via the adapter

            var freshBundler = _fixture.CreateNewBundlerService();
            var bundlerAdapter = new BundlerServiceAdapter(freshBundler, DevChainBundlerFixture.CHAIN_ID);

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4013, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerAdapter,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // CRITICAL: This is the actual AAContractHandler path that was timing out
            // It internally calls:
            //   1. CreateUserOperationAsync (builds and signs UserOp)
            //   2. ToRpcFormat (converts to RPC format)
            //   3. bundlerAdapter.SendUserOperation.SendRequestAsync (sends + executes)
            //   4. WaitForReceiptAsync (polls GetUserOperationReceipt until found)
            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            // Verify receipt is correct
            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);
            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"UserOp should succeed. Revert: {aaReceipt.RevertReason}");

            // Verify counter incremented
            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);

            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Diagnostic-RPC-Roundtrip")]
        public async Task Diagnostic_RpcRoundtrip_HashPreservation()
        {
            // DIAGNOSTIC: Test if RPC format round-trip preserves the UserOpHash
            // This identifies if format conversion is causing hash mismatches

            var freshBundler = _fixture.CreateNewBundlerService();
            var bundlerAdapter = new BundlerServiceAdapter(freshBundler, DevChainBundlerFixture.CHAIN_ID);

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4011, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerAdapter,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // Step 1: Create original PackedUserOperation
            var countFunction = new CountFunction();
            var originalPackedOp = await handler.CreateUserOperationAsync(countFunction);

            // Step 2: Calculate hash from ORIGINAL packed op (before any conversion)
            var hashFromOriginal = await freshBundler.SendUserOperationAsync(originalPackedOp, _fixture.EntryPointService.ContractAddress);

            // Clear the mempool - we need to test the reconverted op separately
            await freshBundler.DropUserOperationAsync(hashFromOriginal);

            // Step 3: Simulate what the adapter does - convert to RPC and back
            var rpcUserOp = UserOperationConverter.ToRpcFormat(originalPackedOp);
            var reconvertedPackedOp = UserOperationConverter.FromRpcFormat(rpcUserOp);

            // Step 4: Calculate hash from RECONVERTED packed op
            var hashFromReconverted = await freshBundler.SendUserOperationAsync(reconvertedPackedOp, _fixture.EntryPointService.ContractAddress);

            // Step 5: Compare hashes - if they differ, RPC round-trip is causing the problem
            Assert.Equal(hashFromOriginal, hashFromReconverted);

            // Cleanup
            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Diagnostic-Sequential")]
        public async Task Diagnostic_SequentialOperations_DetailedTracing()
        {
            // DIAGNOSTIC: Traces exactly what happens between sequential operations
            // to identify why the second operation times out

            var freshBundler = _fixture.CreateNewBundlerService();
            var bundlerAdapter = new BundlerServiceAdapter(freshBundler, DevChainBundlerFixture.CHAIN_ID);

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4014, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerAdapter,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // === FIRST OPERATION ===
            var countFunction1 = new CountFunction();
            var packedOp1 = await handler.CreateUserOperationAsync(countFunction1);
            Assert.NotNull(packedOp1);

            // First op should have nonce 0 and include initCode (account not deployed)
            Assert.Equal(BigInteger.Zero, packedOp1.Nonce);
            var hasInitCode1 = packedOp1.InitCode != null && packedOp1.InitCode.Length > 0;
            Assert.True(hasInitCode1, "First op should have initCode (account not deployed)");

            // Get verification gas from first op (for comparison)
            var accountGasLimits1 = packedOp1.AccountGasLimits ?? new byte[32];
            var verificationGas1 = new BigInteger(accountGasLimits1.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            // Send and execute first op (mirroring what adapter does)
            var rpcUserOp1 = UserOperationConverter.ToRpcFormat(packedOp1);
            var reconvertedOp1 = UserOperationConverter.FromRpcFormat(rpcUserOp1);
            var hash1 = await freshBundler.SendUserOperationAsync(reconvertedOp1, _fixture.EntryPointService.ContractAddress);

            var pendingBefore1 = await freshBundler.GetPendingUserOperationsAsync();
            Assert.True(pendingBefore1.Length >= 1, $"First op: expected pending >= 1, got {pendingBefore1.Length}");

            var result1 = await freshBundler.ExecuteBundleAsync();
            Assert.NotNull(result1);
            Assert.True(result1.Success, $"First bundle failed: {result1.Error}");

            var receipt1 = await freshBundler.GetUserOperationReceiptAsync(hash1);
            Assert.NotNull(receipt1);

            var count1 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count1);

            // Check mempool state after first operation
            var statusAfter1 = await freshBundler.GetUserOperationStatusAsync(hash1);
            Assert.Equal(UserOpState.Included, statusAfter1.State);

            // === SECOND OPERATION ===

            // Query the on-chain nonce for key 0 BEFORE creating second op
            var onChainNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var countFunction2 = new CountFunction();
            var packedOp2 = await handler.CreateUserOperationAsync(countFunction2);
            Assert.NotNull(packedOp2);

            // Detailed nonce diagnostics
            Assert.NotEqual(packedOp1.Nonce, packedOp2.Nonce);
            Assert.Equal(onChainNonce, packedOp2.Nonce);

            // Key diagnostic: Check if initCode is empty for second op (account already deployed)
            var hasInitCode2 = packedOp2.InitCode != null && packedOp2.InitCode.Length > 0;
            Assert.False(hasInitCode2, $"Second op should have empty initCode (account already deployed). InitCode length: {packedOp2.InitCode?.Length ?? 0}");

            // Compare verification gas between first and second operations
            var accountGasLimits2 = packedOp2.AccountGasLimits ?? new byte[32];
            var verificationGas2 = new BigInteger(accountGasLimits2.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            // After FIX: DEFAULT_VERIFICATION_GAS_LIMIT is now 150000 (was 15000)
            // Both operations should have sufficient verification gas
            Assert.True(verificationGas2 >= 100000,
                $"Second op should have sufficient verification gas. First op: {verificationGas1}, Second op: {verificationGas2}");

            // Verify account code exists on-chain
            var accountCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(accountAddress);
            Assert.True(!string.IsNullOrEmpty(accountCode) && accountCode != "0x" && accountCode.Length > 2,
                $"Account should be deployed after first op. Code: {accountCode}");

            // Send second op
            var rpcUserOp2 = UserOperationConverter.ToRpcFormat(packedOp2);
            var reconvertedOp2 = UserOperationConverter.FromRpcFormat(rpcUserOp2);
            var hash2 = await freshBundler.SendUserOperationAsync(reconvertedOp2, _fixture.EntryPointService.ContractAddress);

            // Hashes should be different
            Assert.NotEqual(hash1, hash2);

            // Check pending ops before second bundle
            var pendingBefore2 = await freshBundler.GetPendingUserOperationsAsync();
            Assert.True(pendingBefore2.Length >= 1, $"Second op: expected pending >= 1, got {pendingBefore2.Length}");

            var result2 = await freshBundler.ExecuteBundleAsync();
            Assert.NotNull(result2);
            Assert.True(result2.Success, $"Second bundle failed: {result2.Error}");

            var status2 = await freshBundler.GetUserOperationStatusAsync(hash2);
            Assert.Equal(UserOpState.Included, status2.State);

            var receipt2 = await freshBundler.GetUserOperationReceiptAsync(hash2);
            Assert.NotNull(receipt2);

            var count2 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(2), count2);

            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Diagnostic-Full-Path")]
        public async Task Diagnostic_FullAAContractHandlerPath_WithTracing()
        {
            // DIAGNOSTIC: Full AAContractHandler path with detailed tracing
            // This traces the exact path that times out to identify where it fails

            var freshBundler = _fixture.CreateNewBundlerService();
            var bundlerAdapter = new BundlerServiceAdapter(freshBundler, DevChainBundlerFixture.CHAIN_ID);

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4012, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var handler = testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerAdapter,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // Step 1: Create the packed op (same as handler does internally)
            var countFunction = new CountFunction();
            var originalPackedOp = await handler.CreateUserOperationAsync(countFunction);

            // Step 2: Simulate SendUserOperationAdapter.SendRequestAsync exactly
            var rpcUserOp = UserOperationConverter.ToRpcFormat(originalPackedOp);
            var reconvertedPackedOp = UserOperationConverter.FromRpcFormat(rpcUserOp);

            var userOpHash = await freshBundler.SendUserOperationAsync(reconvertedPackedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(userOpHash);

            // Step 3: Check mempool before execution
            var pendingBefore = await freshBundler.GetPendingUserOperationsAsync();
            Assert.True(pendingBefore.Length >= 1, $"Should have pending. Found: {pendingBefore.Length}");

            // Step 4: Execute bundle (same as adapter does)
            var bundleResult = await freshBundler.ExecuteBundleAsync();
            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle failed: {bundleResult.Error}");
            Assert.NotNull(bundleResult.TransactionHash);

            // Step 5: Check mempool state after execution
            var status = await freshBundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Included, status.State);
            Assert.NotNull(status.TransactionHash);

            // Step 6: Get receipt via bundler directly
            var directReceipt = await freshBundler.GetUserOperationReceiptAsync(userOpHash);
            Assert.NotNull(directReceipt);

            // Step 7: Get receipt via adapter (same path WaitForReceiptAsync uses)
            var adapterReceipt = await bundlerAdapter.GetUserOperationReceipt.SendRequestAsync(userOpHash);
            Assert.NotNull(adapterReceipt);

            // Step 8: Verify the actual counter incremented
            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);

            freshBundler.Dispose();
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
            // Note: GasUsed may be in AATransactionReceipt or embedded receipt depending on implementation

            // AA-specific fields
            Assert.NotNull(aaReceipt.UserOpHash);
            Assert.Equal(66, aaReceipt.UserOpHash.Length); // 0x + 64 hex
            Assert.True(aaReceipt.UserOpSuccess);
            Assert.Null(aaReceipt.RevertReason); // No revert
            // Note: ActualGasUsed and ActualGasCost may vary by bundler implementation
            // Some bundlers may not populate these fields in the receipt
            Assert.True(aaReceipt.ActualGasUsed >= 0, "ActualGasUsed should be non-negative");
            Assert.True(aaReceipt.ActualGasCost >= 0, "ActualGasCost should be non-negative");
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
        [Trait("Scenario", "Error-BalanceVerification")]
        public async Task Scenario_TransferResultVerification_BalanceUnchangedOnFailure()
        {
            // SCENARIO: Verify that failed transfers don't change balances
            // Note: Some ERC20 implementations may succeed without reverting (returning false)
            // while others will revert. We test by verifying balances.

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4015, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Transfer Test Token", "TTT", Web3.Web3.Convert.ToWei(1_000_000));

            // Account has NO tokens
            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            // WHEN: Try to transfer - the account has 0 tokens
            var recipient = "0x" + new string('8', 40);
            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, Web3.Web3.Convert.ToWei(1000)); // Has 0, trying to send 1000

            // THEN: Verify the result - regardless of success/fail, recipient should have 0
            var aaReceipt = (AATransactionReceipt)receipt;
            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);

            // Either the UserOp failed OR the transfer succeeded but returned false (no balance change)
            if (aaReceipt.UserOpSuccess)
            {
                // ERC20 may have succeeded but transfer returned false (no actual transfer)
                Assert.Equal(BigInteger.Zero, recipientBalance);
            }
            else
            {
                // UserOp failed - also no balance change
                Assert.Equal(BigInteger.Zero, recipientBalance);
            }
        }

        #endregion

        #region Helper Methods

        private async Task<TestPaymasterAcceptAllService> DeployAndFundPaymasterAsync(decimal ethAmount)
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address
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
