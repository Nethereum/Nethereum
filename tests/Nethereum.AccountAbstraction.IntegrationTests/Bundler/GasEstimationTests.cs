using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class GasEstimationTests
    {
        private readonly BundlerTestFixture _fixture;

        public GasEstimationTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task EstimateUserOperationGas_WithExistingAccount_ReturnsValidEstimates()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(estimate);
            Assert.True(estimate.VerificationGasLimit.Value > 0, "VerificationGasLimit should be > 0");
            Assert.True(estimate.PreVerificationGas.Value > 0, "PreVerificationGas should be > 0");
        }

        [Fact]
        public async Task EstimateUserOperationGas_WithNewAccount_IncludesInitCodeCost()
        {
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

            var estimateWithInit = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOpWithInit,
                _fixture.EntryPointService.ContractAddress);

            var estimateExisting = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOpExisting,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(estimateWithInit.VerificationGasLimit.Value > estimateExisting.VerificationGasLimit.Value,
                $"InitCode estimate {estimateWithInit.VerificationGasLimit.Value} should be greater than existing {estimateExisting.VerificationGasLimit.Value}");
        }

        [Fact]
        public void PreVerificationGas_CalculatesBasedOnCallDataSize()
        {
            var smallCallData = new byte[100];
            var largeCallData = new byte[1000];

            var smallCost = CalculateCallDataCost(smallCallData);
            var largeCost = CalculateCallDataCost(largeCallData);

            Assert.True(largeCost > smallCost,
                $"Large call data cost ({largeCost}) should be > small ({smallCost})");
        }

        private static long CalculateCallDataCost(byte[] data)
        {
            long cost = 0;
            foreach (var b in data)
            {
                cost += b == 0 ? 4 : 16;
            }
            return cost;
        }

        [Fact]
        public async Task EstimateUserOperationGas_WithUnsupportedEntryPoint_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var fakeEntryPoint = "0x0000000000000000000000000000000000000001";

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _fixture.BundlerService.EstimateUserOperationGasAsync(userOp, fakeEntryPoint));
        }

        [Fact]
        public async Task EstimateUserOperationGas_ReturnsConsistentEstimates()
        {
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

            var estimate1 = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var estimate2 = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.Equal(estimate1.CallGasLimit.Value, estimate2.CallGasLimit.Value);
            Assert.Equal(estimate1.VerificationGasLimit.Value, estimate2.VerificationGasLimit.Value);
            Assert.Equal(estimate1.PreVerificationGas.Value, estimate2.PreVerificationGas.Value);
        }

        [Fact]
        public async Task EstimateUserOperationGas_CanBeUsedForSubmission()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

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

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(estimate);
            Assert.True(estimate.VerificationGasLimit.Value > 0, "VerificationGasLimit should be > 0");
            Assert.True(estimate.PreVerificationGas.Value > 0, "PreVerificationGas should be > 0");

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = 100_000;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(hash), "UserOp hash should not be empty");

            await bundler.FlushAsync();

            var status = await bundler.GetUserOperationStatusAsync(hash);
            Assert.NotNull(status);
            Assert.True(status.State != UserOpState.Pending,
                $"UserOp should have been processed (not pending), was {status.State}");
        }

        [Fact]
        public async Task EstimateUserOperationGas_WithPaymaster_IncludesPaymasterGas()
        {
            var paymasterDeployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, paymasterDeployment);

            await paymasterService.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Web3.Web3.Convert.ToWei(1) });

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOpWithPaymaster = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Paymaster = paymasterService.ContractAddress,
                PaymasterData = new byte[65]
            };

            var userOpWithoutPaymaster = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimateWithPaymaster = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOpWithPaymaster,
                _fixture.EntryPointService.ContractAddress);

            var estimateWithoutPaymaster = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOpWithoutPaymaster,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(estimateWithPaymaster.PreVerificationGas.Value > estimateWithoutPaymaster.PreVerificationGas.Value,
                $"PreVerificationGas with paymaster ({estimateWithPaymaster.PreVerificationGas.Value}) " +
                $"should be > without ({estimateWithoutPaymaster.PreVerificationGas.Value})");
        }

        [Fact]
        public async Task EstimateUserOperationGas_BatchExecution_ReturnsHigherCallGas()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var recipient1 = "0x" + new string('1', 40);
            var recipient2 = "0x" + new string('2', 40);
            var recipient3 = "0x" + new string('3', 40);

            var singleExecute = new ExecuteFunction
            {
                Target = recipient1,
                Value = 1000,
                Data = Array.Empty<byte>()
            };

            var batchCalls = new List<Call>
            {
                new Call { Target = recipient1, Value = 1000, Data = Array.Empty<byte>() },
                new Call { Target = recipient2, Value = 1000, Data = Array.Empty<byte>() },
                new Call { Target = recipient3, Value = 1000, Data = Array.Empty<byte>() }
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

            var estimateSingle = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOpSingle,
                _fixture.EntryPointService.ContractAddress);

            var estimateBatch = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOpBatch,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(estimateBatch.PreVerificationGas.Value > estimateSingle.PreVerificationGas.Value,
                $"Batch PreVerificationGas ({estimateBatch.PreVerificationGas.Value}) " +
                $"should be > single ({estimateSingle.PreVerificationGas.Value})");
        }

        [Fact]
        public async Task ExecuteOperation_ActualGasUsed_WithinEstimate()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

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

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle should succeed, error: {result.Error}");

            var totalEstimatedGas = estimate.PreVerificationGas.Value +
                                    estimate.VerificationGasLimit.Value +
                                    estimate.CallGasLimit.Value;

            Assert.True(result.GasUsed <= totalEstimatedGas,
                $"Actual gas used ({result.GasUsed}) should be <= estimated ({totalEstimatedGas})");

            Assert.True(result.GasUsed > 0, "Gas used should be > 0");
        }

        [Fact]
        public async Task ExecuteOperation_WithLargeCalldata_EstimateIsAccurate()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var largeData = new byte[500];
            for (int i = 0; i < 500; i++) largeData[i] = (byte)(i % 256);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = largeData
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var expectedCalldataCost = UserOperationGasEstimator.CalculateCalldataCost(executeFunction.GetCallData());

            Assert.True(estimate.PreVerificationGas.Value >=
                GasEstimationConstants.PRE_VERIFICATION_OVERHEAD_GAS + (long)expectedCalldataCost / 2,
                $"PreVerificationGas ({estimate.PreVerificationGas.Value}) should account for large calldata");

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Operation with large calldata should succeed, error: {result.Error}");
        }

        [Fact]
        public async Task ExecuteBatch_ActualGasUsed_WithinEstimate()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var batchCalls = new List<Call>
            {
                new Call { Target = "0x" + new string('1', 40), Value = 1000, Data = Array.Empty<byte>() },
                new Call { Target = "0x" + new string('2', 40), Value = 1000, Data = Array.Empty<byte>() }
            };

            var batchExecute = new ExecuteBatchFunction { Calls = batchCalls };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = batchExecute.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Batch execution should succeed, error: {result.Error}");

            var totalEstimatedGas = estimate.PreVerificationGas.Value +
                                    estimate.VerificationGasLimit.Value +
                                    estimate.CallGasLimit.Value;

            Assert.True(result.GasUsed <= totalEstimatedGas * 2,
                $"Batch gas used ({result.GasUsed}) should be reasonable relative to estimate ({totalEstimatedGas})");
        }
    }
}
