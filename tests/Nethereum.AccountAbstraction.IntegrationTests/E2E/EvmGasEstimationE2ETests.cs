using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC20;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EvmGasEstimation")]
    [Trait("Category", "E2E")]
    [Trait("ERC", "4337")]
    public class EvmGasEstimationE2ETests
    {
        private readonly DevChainBundlerFixture _fixture;

        public EvmGasEstimationE2ETests(DevChainBundlerFixture fixture)
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

        private TransactionExecutorGasEstimator CreateEvmGasEstimator()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            return new TransactionExecutorGasEstimator(
                nodeDataService,
                DevChainBundlerFixture.CHAIN_ID,
                HardforkConfig.Default);
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

        private async Task<TestPaymasterAcceptAllService> DeployAndFundPaymasterAsync(decimal ethAmount)
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.OperatorAccount.Address
            };

            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            await paymasterService.AddStakeRequestAndWaitForReceiptAsync(
                new AddStakeFunction
                {
                    UnstakeDelaySec = 86400,
                    AmountToSend = Web3.Web3.Convert.ToWei(0.1m)
                });

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = paymasterService.ContractAddress,
                    AmountToSend = Web3.Web3.Convert.ToWei(ethAmount)
                });

            return paymasterService;
        }

        [Fact]
        [Trait("Scenario", "ERC20-AAHandler-Transfer")]
        public async Task Given_ERC20WithAAHandler_When_TransferExecuted_Then_BalanceChangesCorrectly()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(5001, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "AA Handler Token", "AAHT", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(500));

            var initialBalance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(500), initialBalance);

            var bundlerService = CreateBundlerAdapter();

            erc20Service.SwitchToAccountAbstraction(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var recipient = "0x" + new string('5', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(50);
            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(recipient, transferAmount);

            Assert.NotNull(receipt);
            Assert.IsType<AATransactionReceipt>(receipt);

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Transfer should succeed. Revert: {aaReceipt.RevertReason}");

            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var senderBalance = await erc20Service.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(450), senderBalance);
        }

        [Fact]
        [Trait("Scenario", "ERC20-EvmEstimate-Compare")]
        public async Task Given_ERC20Transfer_When_ComparedEvmToNode_Then_EvmProvidesSufficientGas()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(5002, 10m);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "EVM Compare Token", "EVMC", Web3.Web3.Convert.ToWei(1_000_000));

            await erc20Service.TransferRequestAndWaitForReceiptAsync(
                accountAddress, Web3.Web3.Convert.ToWei(500));

            var evmEstimator = CreateEvmGasEstimator();
            var evmGasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.OperatorAccount.Address);

            var nodeGasEstimator = new UserOperationGasEstimator(
                _fixture.Web3,
                _fixture.EntryPointService.ContractAddress,
                _fixture.OperatorAccount.Address);

            var recipient = "0x" + new string('6', 40);
            var transferFunc = new Nethereum.StandardTokenEIP20.ContractDefinition.TransferFunction
            {
                To = recipient,
                Value = Web3.Web3.Convert.ToWei(50)
            };

            var executeFunc = new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction
            {
                Target = tokenAddress,
                Value = 0,
                Data = transferFunc.GetCallData()
            };

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(
                accountKey.GetPublicAddress(), factoryConfig.Salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                InitCode = initCode,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var evmEstimate = await evmGasEstimator.EstimateGasAsync(userOp);
            var nodeEstimate = await nodeGasEstimator.EstimateGasAsync(userOp);

            Assert.Equal(evmEstimate.PreVerificationGas, nodeEstimate.PreVerificationGas);

            Assert.True(evmEstimate.VerificationGasLimit > 0,
                $"EVM VerificationGasLimit should be > 0, got {evmEstimate.VerificationGasLimit}");
            Assert.True(evmEstimate.CallGasLimit > 0,
                $"EVM CallGasLimit should be > 0, got {evmEstimate.CallGasLimit}");

            var verificationDiff = Math.Abs((double)(evmEstimate.VerificationGasLimit - nodeEstimate.VerificationGasLimit));
            var maxVerification = (double)BigInteger.Max(evmEstimate.VerificationGasLimit, nodeEstimate.VerificationGasLimit);
            var verificationRatio = maxVerification > 0 ? verificationDiff / maxVerification : 0;

            Assert.True(verificationRatio < 0.5,
                $"Verification gas should be within 50%: EVM={evmEstimate.VerificationGasLimit}, Node={nodeEstimate.VerificationGasLimit}");
        }

        [Fact]
        [Trait("Scenario", "Counter-MultipleOps")]
        public async Task Given_CounterContract_When_MultipleOpsWithAAHandler_Then_AllSucceed()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(5003, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var bundlerService = CreateBundlerAdapter();

            testCounter.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                bundlerService,
                _fixture.EntryPointService.ContractAddress,
                factory: factoryConfig);

            var receipt1 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt1).UserOpSuccess, "First count should succeed");

            var count1 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.One, count1);

            var receipt2 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt2).UserOpSuccess, "Second count should succeed");

            var count2 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(2), count2);

            var receipt3 = await testCounter.CountRequestAndWaitForReceiptAsync();
            Assert.True(((AATransactionReceipt)receipt3).UserOpSuccess, "Third count should succeed");

            var count3 = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count3);
        }

        [Fact]
        [Trait("Scenario", "Paymaster-Sponsorship")]
        public async Task Given_PaymasterSponsorship_When_TransferExecuted_Then_PaymasterPaysGas()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var accountKey = EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();
            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, 5004);

            var factoryConfig = new FactoryConfig(
                _fixture.AccountFactoryService.ContractAddress,
                ownerAddress,
                5004);

            var (tokenAddress, erc20Service) = await DeployERC20TokenAsync(
                "Paymaster Token", "PMT", Web3.Web3.Convert.ToWei(1_000_000));

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

            var recipient = "0x" + new string('7', 40);
            var receipt = await erc20Service.TransferRequestAndWaitForReceiptAsync(
                recipient, Web3.Web3.Convert.ToWei(10));

            var aaReceipt = (AATransactionReceipt)receipt;
            Assert.True(aaReceipt.UserOpSuccess, $"Paymaster-sponsored transfer should succeed. Revert: {aaReceipt.RevertReason}");

            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(Web3.Web3.Convert.ToWei(10), recipientBalance);

            var accountEthBalance = await _fixture.GetBalanceAsync(accountAddress);
            Assert.Equal(BigInteger.Zero, accountEthBalance);
        }

        [Fact]
        [Trait("Scenario", "Batch-Execution")]
        public async Task Given_BatchExecution_When_MultipleCallsExecuted_Then_AllSucceed()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(5005, 10m);

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

            Assert.True(receipt.UserOpSuccess, $"Batch should succeed. Revert: {receipt.RevertReason}");

            var count = await testCounter.CountersQueryAsync(accountAddress);
            Assert.Equal(new BigInteger(3), count);
        }

        [Fact]
        [Trait("Scenario", "EVM-Estimate-UserOp")]
        public async Task Given_EvmEstimator_When_EstimateUserOp_Then_ReturnsValidGasLimits()
        {
            var (accountAddress, accountKey, factoryConfig) = await CreateAccountWithFactoryAsync(5006, 10m);

            var testCounter = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var evmEstimator = CreateEvmGasEstimator();
            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.OperatorAccount.Address);

            var countFunc = new CountFunction();
            var executeFunc = new Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction
            {
                Target = testCounter.ContractAddress,
                Value = 0,
                Data = countFunc.GetCallData()
            };

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(
                accountKey.GetPublicAddress(), factoryConfig.Salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunc.GetCallData(),
                InitCode = initCode,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await gasEstimator.EstimateGasAsync(userOp);

            Assert.True(estimate.VerificationGasLimit >= GasEstimationConstants.VERIFICATION_GAS_BUFFER,
                $"VerificationGasLimit ({estimate.VerificationGasLimit}) should be >= buffer ({GasEstimationConstants.VERIFICATION_GAS_BUFFER})");
            Assert.True(estimate.PreVerificationGas > 0,
                $"PreVerificationGas should be > 0, got {estimate.PreVerificationGas}");
            Assert.True(estimate.CallGasLimit >= GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT,
                $"CallGasLimit ({estimate.CallGasLimit}) should be >= default ({GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT})");
        }

        [Fact]
        [Trait("Scenario", "ERC20-Approve-TransferFrom")]
        public async Task Given_ERC20ApproveAndTransferFrom_When_ExecutedWithAA_Then_BothSucceed()
        {
            var (ownerAddress, ownerKey, ownerFactory) = await CreateAccountWithFactoryAsync(5007, 10m);
            var (spenderAddress, spenderKey, spenderFactory) = await CreateAccountWithFactoryAsync(5008, 10m);

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

            var recipient = "0x" + new string('8', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(200);

            var transferFromReceipt = await erc20Service.TransferFromRequestAndWaitForReceiptAsync(
                ownerAddress, recipient, transferAmount);

            Assert.True(((AATransactionReceipt)transferFromReceipt).UserOpSuccess, "TransferFrom should succeed");

            var recipientBalance = await erc20Service.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var newAllowance = await erc20Service.AllowanceQueryAsync(ownerAddress, spenderAddress);
            Assert.Equal(approvalAmount - transferAmount, newAllowance);
        }
    }
}
