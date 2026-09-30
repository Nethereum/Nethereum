using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
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
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "ERC20 transfer using ERC20ContractService with AA", Order = 10)]
        public async Task Scenario_ERC20Transfer_UsingERC20ContractService_WithAA()
        {

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4001, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "AA Test Token", "AAT", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(1000));

            var initialBalance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(1000), initialBalance);

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

            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Transfer should succeed. Revert: {aaReceipt.RevertReason}");

            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var senderBalance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(900), senderBalance);
        }

        [Fact]
        [Trait("Scenario", "ERC20-Approve-TransferFrom")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "ERC20 approve and transferFrom with AA", Order = 11)]
        public async Task Scenario_ERC20ApproveAndTransferFrom_WithAA()
        {

            var (ownerAddress, ownerKey, ownerFactory) = await CreateAccountWithFactoryAsync(4002, 10m);
            var (spenderAddress, spenderKey, spenderFactory) = await CreateAccountWithFactoryAsync(4003, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Approve Test Token", "APT", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                ownerAddress, Web3.Web3.Convert.ToWei(1000));

            var bundlerService = CreateBundlerAdapter();

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

            var allowance = await erc20Service.AllowanceQueryAsync(ownerAddress, spenderAddress);
            Assert.Equal(approvalAmount, allowance);

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
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "SwitchToAccountAbstraction on web3.Eth.ERC20 built-in service", Order = 12)]
        public async Task Scenario_Web3EthERC20_SwitchToAccountAbstraction()
        {

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4004, 10m);

            var (tokenAddress, _) = await DeployERC20TokenAsync(
                "Web3 ERC20 Token", "W3T", Web3.Web3.Convert.ToWei(1_000_000));

            var web3 = (Web3.Web3)_fixture.Web3;
            var erc20Service = web3.Eth.ERC20.GetContractService(tokenAddress);

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(500));

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var recipient = "0x" + new string('3', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(50);

            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, transferAmount);

            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);

            var balance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, balance);
        }

        #endregion

        #region Scenario 3: Paymaster Integration

        [Fact]
        [Trait("Scenario", "Paymaster-Sponsorship")]
        public async Task Scenario_PaymasterSponsorship_WithAAHandler()
        {

            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, 4005);


            var factoryConfig = new FactoryConfig(
                _fixture.AccountFactoryService.ContractAddress,
                ownerAddress,
                4005);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Paymaster Test Token", "PMT", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(100));

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig)
                .WithPaymaster(paymasterService.ContractAddress);

            var recipient = "0x" + new string('4', 40);
            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, Web3.Web3.Convert.ToWei(10));

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Paymaster-sponsored transfer should succeed. Revert: {aaReceipt.RevertReason}");

            var balance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(Web3.Web3.Convert.ToWei(10), balance);
        }

        [Fact]
        [Trait("Scenario", "Paymaster-WithData")]
        public async Task Scenario_PaymasterWithStaticData_WithAAHandler()
        {

            var paymasterService = await DeployAndFundPaymasterAsync(5m);
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4006, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var paymasterData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig)
                .WithPaymaster(paymasterService.ContractAddress, paymasterData);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Paymaster-sponsored operation should succeed. Revert: {aaReceipt.RevertReason}");
        }

        #endregion

        #region Scenario 4: Batch Execution

        [Fact]
        [Trait("Scenario", "Batch-MultipleContracts")]
        [NethereumDocExample(DocSection.AccountAbstraction, "batching-and-paymasters", "Batch multiple calls to one contract atomically", Order = 4)]
        public async Task Scenario_BatchExecution_MultipleContractCalls()
        {

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4007, 10m);

            var counter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            var handler = counter.ChangeContractHandlerToAA(
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

            Assert.True(receipt.UserOpSuccess, $"Batch should succeed. Revert: {receipt.RevertReason}");

            var count = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count);
        }

        [Fact]
        [Trait("Scenario", "Batch-MultipleOperations")]
        [NethereumDocExample(DocSection.AccountAbstraction, "batching-and-paymasters", "Batch execution with mixed operations", Order = 5)]
        public async Task Scenario_BatchExecution_MixedOperations()
        {

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

            var receipt = await handler.BatchExecuteAsync(
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall(),
                new CountFunction().ToBatchCall());

            Assert.True(receipt.UserOpSuccess, $"Batch should succeed. Revert: {receipt.RevertReason}");

            var finalCount = await counter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(5), finalCount);
        }

        #endregion

        #region Scenario 5: Sequential Operations and Nonce Management

        [Fact]
        [Trait("Scenario", "Sequential-Operations")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Sequential operations with nonce increments", Order = 13)]
        public async Task Scenario_SequentialOperations_NonceIncrements()
        {

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

            var receipt1 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.NotNull(receipt1);
            Assert.IsType<AATransactionReceipt>(receipt1);
            Assert.True(((AATransactionReceipt)receipt1).UserOpSuccess, "First count should succeed");

            var count1 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count1);

            var receipt2 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.NotNull(receipt2);
            Assert.True(((AATransactionReceipt)receipt2).UserOpSuccess, "Second count should succeed");

            var count2 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(2), count2);

            var receipt3 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.NotNull(receipt3);
            Assert.True(((AATransactionReceipt)receipt3).UserOpSuccess, "Third count should succeed");

            var count3 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count3);

            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Full-AAContractHandler-Path")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Full AAContractHandler path with CountRequestAndWaitForReceipt", Order = 14)]
        public async Task Scenario_FullAAContractHandler_CountRequestAndWaitForReceipt()
        {

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

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);
            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"UserOp should succeed. Revert: {aaReceipt.RevertReason}");

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);

            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Diagnostic-RPC-Roundtrip")]
        public async Task Diagnostic_RpcRoundtrip_HashPreservation()
        {

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

            var countFunction = new CountFunction();
            var originalPackedOp = await handler.CreateUserOperationAsync(countFunction);

            var hashFromOriginal = await freshBundler.SendUserOperationAsync(originalPackedOp, _fixture.EntryPointService.ContractAddress);

            await freshBundler.DropUserOperationAsync(hashFromOriginal);

            var rpcUserOp = UserOperationConverter.ToRpcFormat(originalPackedOp);
            var reconvertedPackedOp = UserOperationConverter.FromRpcFormat(rpcUserOp);

            var hashFromReconverted = await freshBundler.SendUserOperationAsync(reconvertedPackedOp, _fixture.EntryPointService.ContractAddress);

            Assert.Equal(hashFromOriginal, hashFromReconverted);

            freshBundler.Dispose();
        }

        [Fact]
        [Trait("Scenario", "Diagnostic-Sequential")]
        public async Task Diagnostic_SequentialOperations_DetailedTracing()
        {

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

            var countFunction1 = new CountFunction();
            var packedOp1 = await handler.CreateUserOperationAsync(countFunction1);
            Assert.NotNull(packedOp1);

            Assert.Equal(BigInteger.Zero, packedOp1.Nonce);
            var hasInitCode1 = packedOp1.InitCode != null && packedOp1.InitCode.Length > 0;
            Assert.True(hasInitCode1, "First op should have initCode (account not deployed)");

            var accountGasLimits1 = packedOp1.AccountGasLimits ?? new byte[32];
            var verificationGas1 = new BigInteger(accountGasLimits1.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

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

            var statusAfter1 = await freshBundler.GetUserOperationStatusAsync(hash1);
            Assert.Equal(UserOpState.Included, statusAfter1.State);


            var onChainNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var countFunction2 = new CountFunction();
            var packedOp2 = await handler.CreateUserOperationAsync(countFunction2);
            Assert.NotNull(packedOp2);

            Assert.NotEqual(packedOp1.Nonce, packedOp2.Nonce);
            Assert.Equal(onChainNonce, packedOp2.Nonce);

            var hasInitCode2 = packedOp2.InitCode != null && packedOp2.InitCode.Length > 0;
            Assert.False(hasInitCode2, $"Second op should have empty initCode (account already deployed). InitCode length: {packedOp2.InitCode?.Length ?? 0}");

            var accountGasLimits2 = packedOp2.AccountGasLimits ?? new byte[32];
            var verificationGas2 = new BigInteger(accountGasLimits2.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            Assert.True(verificationGas2 > 0,
                $"Second op verification gas should be positive, got {verificationGas2}");
            Assert.True(verificationGas2 < verificationGas1,
                $"Second op (no deploy) should need less verification gas than the first (deploys). First: {verificationGas1}, Second: {verificationGas2}");

            var accountCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(accountAddress);
            Assert.True(!string.IsNullOrEmpty(accountCode) && accountCode != "0x" && accountCode.Length > 2,
                $"Account should be deployed after first op. Code: {accountCode}");

            var rpcUserOp2 = UserOperationConverter.ToRpcFormat(packedOp2);
            var reconvertedOp2 = UserOperationConverter.FromRpcFormat(rpcUserOp2);
            var hash2 = await freshBundler.SendUserOperationAsync(reconvertedOp2, _fixture.EntryPointService.ContractAddress);

            Assert.NotEqual(hash1, hash2);

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

            var countFunction = new CountFunction();
            var originalPackedOp = await handler.CreateUserOperationAsync(countFunction);

            var rpcUserOp = UserOperationConverter.ToRpcFormat(originalPackedOp);
            var reconvertedPackedOp = UserOperationConverter.FromRpcFormat(rpcUserOp);

            var userOpHash = await freshBundler.SendUserOperationAsync(reconvertedPackedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(userOpHash);

            var pendingBefore = await freshBundler.GetPendingUserOperationsAsync();
            Assert.True(pendingBefore.Length >= 1, $"Should have pending. Found: {pendingBefore.Length}");

            var bundleResult = await freshBundler.ExecuteBundleAsync();
            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle failed: {bundleResult.Error}");
            Assert.NotNull(bundleResult.TransactionHash);

            var status = await freshBundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Included, status.State);
            Assert.NotNull(status.TransactionHash);

            var directReceipt = await freshBundler.GetUserOperationReceiptAsync(userOpHash);
            Assert.NotNull(directReceipt);

            var adapterReceipt = await bundlerAdapter.GetUserOperationReceipt.SendRequestAsync(userOpHash);
            Assert.NotNull(adapterReceipt);

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count);

            freshBundler.Dispose();
        }

        #endregion

        #region Scenario 6: Receipt Inspection

        [Fact]
        [Trait("Scenario", "Receipt-Details")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Inspect AATransactionReceipt fields", Order = 15)]
        public async Task Scenario_InspectAAReceipt_AllFieldsPopulated()
        {

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

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();

            Assert.IsType<AATransactionReceipt>(receipt);
            var aaReceipt = (AATransactionReceipt)receipt;

            Assert.NotNull(aaReceipt.TransactionHash);
            Assert.NotNull(aaReceipt.BlockNumber);
            Assert.NotNull(aaReceipt.BlockHash);

            Assert.NotNull(aaReceipt.UserOpHash);
            Assert.Equal(66, aaReceipt.UserOpHash.Length);
            Assert.True(aaReceipt.UserOpSuccess);
            Assert.Null(aaReceipt.RevertReason);
            Assert.True(aaReceipt.ActualGasUsed >= 0, "ActualGasUsed should be non-negative");
            Assert.True(aaReceipt.ActualGasCost >= 0, "ActualGasCost should be non-negative");
            Assert.Equal(accountAddress.ToLower(), aaReceipt.Sender?.ToLower());
        }

        #endregion

        #region Scenario 7: Configuration Variations

        [Fact]
        [Trait("Scenario", "Config-GasSettings")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Custom gas config with AAGasConfig", Order = 16)]
        public async Task Scenario_CustomGasConfig_AffectsPolling()
        {

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4011, 10m);

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
                    ReceiptPollIntervalMs = 100,
                    ReceiptTimeoutMs = 60000
                });

            Assert.Equal(100, handler.GasConfig.ReceiptPollIntervalMs);
            Assert.Equal(60000, handler.GasConfig.ReceiptTimeoutMs);

            var receipt = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt).UserOpSuccess);
        }

        [Fact(Skip = "Paymaster gas estimation is deferred to B2-8 (paymaster external proof): the real simulateHandleOp paymaster path reverts AA33 on DevChain. See docs/internal/aa-spec-conformance-matrix.md WBS B2-P2/B2-8.")]
        [Trait("Scenario", "Config-FullFluent")]
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Full fluent configuration of AAContractHandler", Order = 17)]
        public async Task Scenario_FluentConfiguration_AllOptions()
        {

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4012, 10m);
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

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

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4013, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Query Token", "QRY", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(100));

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var name = await erc20Service.NameQueryAsync();
            var symbol = await erc20Service.SymbolQueryAsync();
            var decimals = await erc20Service.DecimalsQueryAsync();
            var balance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            var totalSupply = await erc20Service.TotalSupplyQueryAsync();

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
        [NethereumDocExample(DocSection.AccountAbstraction, "smart-contracts-with-aa", "Create UserOperation for inspection without submitting", Order = 18)]
        public async Task Scenario_CreateUserOperationForInspection()
        {

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

            var countFunction = new CountFunction();
            var packedOp = await handler.CreateUserOperationAsync(countFunction);

            Assert.Equal(accountAddress.ToLower(), packedOp.Sender.ToLower());
            Assert.NotNull(packedOp.CallData);
            Assert.True(packedOp.CallData.Length > 0);
            Assert.NotNull(packedOp.Signature);
            Assert.True(packedOp.Signature.Length > 0);

            Assert.NotNull(packedOp.InitCode);
            Assert.True(packedOp.InitCode.Length > 0);

            Assert.True(packedOp.PreVerificationGas > 0);
        }

        #endregion

        #region Scenario 10: Error Handling

        [Fact]
        [Trait("Scenario", "Error-BalanceVerification")]
        public async Task Scenario_TransferResultVerification_BalanceUnchangedOnFailure()
        {

            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(4015, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Transfer Test Token", "TTT", Web3.Web3.Convert.ToWei(1_000_000));

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var recipient = "0x" + new string('8', 40);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                erc20Service.TransferRequestAndWaitForReceiptAsync(recipient, Web3.Web3.Convert.ToWei(1000)));

            var rpcEx = Assert.IsType<Nethereum.JsonRpc.Client.RpcResponseException>(ex.InnerException);
            Assert.Equal(Nethereum.AccountAbstraction.Bundler.BundlerErrorCodes.UserOperationReverted, rpcEx.RpcError.Code);

            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(BigInteger.Zero, recipientBalance);
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

            await paymasterService.AddStakeRequestAndWaitForReceiptAsync(
                new AddStakeFunction
                {
                    UnstakeDelaySec = 86400,
                    AmountToSend = Web3.Web3.Convert.ToWei(0.1m)
                });

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
