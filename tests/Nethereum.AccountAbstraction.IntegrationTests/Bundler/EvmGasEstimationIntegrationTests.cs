using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class EvmGasEstimationIntegrationTests
    {
        private readonly BundlerTestFixture _fixture;

        public EvmGasEstimationIntegrationTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TransactionExecutorGasEstimator_DirectEoaCall_ReturnsGasEstimate()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var targetAddress = "0x" + new string('4', 40);

            var result = await evmEstimator.EstimateGasAsync(
                _fixture.BeneficiaryAddress,
                targetAddress,
                Array.Empty<byte>(),
                BigInteger.Zero,
                GasEstimationConstants.MAX_SIMULATION_GAS);

            Assert.True(result.Success, $"EVM estimation should succeed, error: {result.Error}");
            Assert.True(result.GasUsed > 0, "Gas used should be > 0");
            Assert.True(result.GasUsed < GasEstimationConstants.MAX_SIMULATION_GAS,
                $"Gas used ({result.GasUsed}) should be less than max simulation gas");
        }

        [Fact]
        public async Task UserOperationGasEstimator_WithEvmEstimator_EstimatesCorrectly()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var result = await gasEstimator.EstimateGasAsync(userOp);

            Assert.True(result.VerificationGasLimit >= GasEstimationConstants.VERIFICATION_GAS_BUFFER,
                $"VerificationGasLimit ({result.VerificationGasLimit}) should be >= buffer ({GasEstimationConstants.VERIFICATION_GAS_BUFFER})");
            Assert.True(result.PreVerificationGas > 0, "PreVerificationGas should be > 0");
            Assert.True(result.CallGasLimit >= GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT,
                $"CallGasLimit ({result.CallGasLimit}) should be >= default ({GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT})");
        }

        [Fact]
        public async Task EvmEstimator_ComparedToNodeRpc_ReturnsComparableResults()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var evmGasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var nodeGasEstimator = new UserOperationGasEstimator(
                _fixture.Web3,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var evmResult = await evmGasEstimator.EstimateGasAsync(userOp);
            var nodeResult = await nodeGasEstimator.EstimateGasAsync(userOp);

            Assert.Equal(evmResult.PreVerificationGas, nodeResult.PreVerificationGas);

            var verificationDiff = Math.Abs((double)(evmResult.VerificationGasLimit - nodeResult.VerificationGasLimit));
            var maxVerification = (double)BigInteger.Max(evmResult.VerificationGasLimit, nodeResult.VerificationGasLimit);
            var verificationRatio = verificationDiff / maxVerification;

            Assert.True(verificationRatio < 0.5,
                $"Verification gas should be within 50%: EVM={evmResult.VerificationGasLimit}, Node={nodeResult.VerificationGasLimit}");
        }

        [Fact]
        public async Task EvmEstimator_WithInitCode_EstimatesDeploymentGas()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt1 = (ulong)Random.Shared.NextInt64();
            var salt2 = (ulong)Random.Shared.NextInt64();

            var (existingAccountAddress, _) = await _fixture.CreateFundedAccountAsync(salt1);

            var newOwnerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var newOwnerAddress = newOwnerKey.GetPublicAddress();
            var newAccountAddress = await _fixture.GetAccountAddressAsync(newOwnerAddress, salt2);
            await _fixture.FundAccountAsync(newAccountAddress, 0.1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(newOwnerAddress, salt2);

            var executeFunction = new ExecuteFunction
            {
                Target = newAccountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOpWithInit = new UserOperation
            {
                Sender = newAccountAddress,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOpExisting = new UserOperation
            {
                Sender = existingAccountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimateWithInit = await gasEstimator.EstimateGasAsync(userOpWithInit);
            var estimateExisting = await gasEstimator.EstimateGasAsync(userOpExisting);

            Assert.True(estimateWithInit.VerificationGasLimit > estimateExisting.VerificationGasLimit,
                $"InitCode estimate ({estimateWithInit.VerificationGasLimit}) should be > existing ({estimateExisting.VerificationGasLimit})");

            Assert.True(estimateWithInit.PreVerificationGas > estimateExisting.PreVerificationGas,
                $"PreVerificationGas with initCode ({estimateWithInit.PreVerificationGas}) should be > existing ({estimateExisting.PreVerificationGas})");
        }

        [Fact]
        public async Task EvmEstimator_WithBatchCall_EstimatesHigherGas()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var singleExecute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var batchCalls = new List<Call>
            {
                new Call { Target = accountAddress, Value = 0, Data = Array.Empty<byte>() },
                new Call { Target = accountAddress, Value = 0, Data = Array.Empty<byte>() },
                new Call { Target = accountAddress, Value = 0, Data = Array.Empty<byte>() }
            };

            var batchExecute = new ExecuteBatchFunction { Calls = batchCalls };

            var userOpSingle = new UserOperation
            {
                Sender = accountAddress,
                CallData = singleExecute.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOpBatch = new UserOperation
            {
                Sender = accountAddress,
                CallData = batchExecute.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimateSingle = await gasEstimator.EstimateGasAsync(userOpSingle);
            var estimateBatch = await gasEstimator.EstimateGasAsync(userOpBatch);

            Assert.True(estimateBatch.PreVerificationGas > estimateSingle.PreVerificationGas,
                $"Batch PreVerificationGas ({estimateBatch.PreVerificationGas}) should be > single ({estimateSingle.PreVerificationGas})");
        }

        [Fact]
        public async Task EvmEstimator_WithPaymaster_SetsPaymasterGasLimits()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Paymaster = "0xAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                PaymasterData = new byte[65]
            };

            var result = await gasEstimator.EstimateGasAsync(userOp);

            Assert.Equal(GasEstimationConstants.DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK,
                result.PaymasterVerificationGasLimit);
            Assert.Equal(GasEstimationConstants.DEFAULT_PAYMASTER_POST_OP_GAS_FALLBACK,
                result.PaymasterPostOpGasLimit);
        }

        [Fact]
        public async Task EvmEstimator_EstimatedGas_SufficientForExecution()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var recipient = "0x" + new string('4', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(0.001m);

            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = transferAmount,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await gasEstimator.EstimateGasAsync(userOp);

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas;
            userOp.CallGasLimit = (long)estimate.CallGasLimit;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Execution should succeed with EVM-estimated gas, error: {result.Error}");

            var totalEstimatedGas = estimate.PreVerificationGas +
                                    estimate.VerificationGasLimit +
                                    estimate.CallGasLimit;

            Assert.True(result.GasUsed <= totalEstimatedGas,
                $"Actual gas ({result.GasUsed}) should be <= estimated ({totalEstimatedGas})");
        }

        [Fact]
        public async Task EvmEstimator_ConsistentResults_MultipleCalls()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var result1 = await gasEstimator.EstimateGasAsync(userOp);
            var result2 = await gasEstimator.EstimateGasAsync(userOp);
            var result3 = await gasEstimator.EstimateGasAsync(userOp);

            Assert.Equal(result1.PreVerificationGas, result2.PreVerificationGas);
            Assert.Equal(result2.PreVerificationGas, result3.PreVerificationGas);

            Assert.Equal(result1.VerificationGasLimit, result2.VerificationGasLimit);
            Assert.Equal(result2.VerificationGasLimit, result3.VerificationGasLimit);

            Assert.Equal(result1.CallGasLimit, result2.CallGasLimit);
            Assert.Equal(result2.CallGasLimit, result3.CallGasLimit);
        }

        [Fact]
        public async Task EvmEstimator_LargeCalldata_EstimatesCorrectly()
        {
            var nodeDataService = new RpcNodeDataService(_fixture.Web3.Eth, BlockParameter.CreateLatest());
            var evmEstimator = new TransactionExecutorGasEstimator(
                nodeDataService,
                _fixture.ChainId,
                HardforkConfig.Default);

            var gasEstimator = new UserOperationGasEstimator(
                evmEstimator,
                _fixture.EntryPointService.ContractAddress,
                _fixture.BeneficiaryAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var smallData = new byte[10];
            var largeData = new byte[500];
            for (int i = 0; i < 500; i++) largeData[i] = (byte)(i % 256);

            var smallExecute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = smallData
            };

            var largeExecute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = largeData
            };

            var userOpSmall = new UserOperation
            {
                Sender = accountAddress,
                CallData = smallExecute.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOpLarge = new UserOperation
            {
                Sender = accountAddress,
                CallData = largeExecute.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimateSmall = await gasEstimator.EstimateGasAsync(userOpSmall);
            var estimateLarge = await gasEstimator.EstimateGasAsync(userOpLarge);

            Assert.True(estimateLarge.PreVerificationGas > estimateSmall.PreVerificationGas,
                $"Large calldata PreVerificationGas ({estimateLarge.PreVerificationGas}) should be > small ({estimateSmall.PreVerificationGas})");
        }
    }
}
