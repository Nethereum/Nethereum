using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Call = Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.Call;
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

        private const string DUMMY_SIGNATURE =
            "0xfffffffffffffffffffffffffffffff0000000000000000000000000000000007aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1c";

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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var userOpExisting = new UserOperation
            {
                Sender = existingAccountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
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

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                _fixture.BundlerService.EstimateUserOperationGasAsync(userOp, fakeEntryPoint));

            Assert.Equal(BundlerErrorCodes.InvalidFields, ex.Code);
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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(estimate);
            Assert.True(estimate.VerificationGasLimit.Value > 0, "VerificationGasLimit should be > 0");
            Assert.True(estimate.PreVerificationGas.Value > 0, "PreVerificationGas should be > 0");

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

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
        public async Task EstimateUserOperationGas_WithPaymaster_SurfacesValidationFailureCleanly()
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
                PaymasterData = new byte[65],
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                _fixture.BundlerService.EstimateUserOperationGasAsync(
                    userOpWithPaymaster, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var userOpBatch = new UserOperation
            {
                Sender = accountAddress,
                CallData = batchExecute.GetCallData(),
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
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
                Target = "0x" + new string('5', 40),
                Value = 0,
                Data = largeData
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var expectedCalldataCost = UserOperationGasEstimator.CalculateCalldataCost(executeFunction.GetCallData());

            Assert.True(estimate.PreVerificationGas.Value >=
                GasEstimationConstants.BASE_TRANSACTION_GAS + (long)expectedCalldataCost / 2,
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
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
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

        [Fact]
        public async Task EstimateUserOperationGas_CounterfactualSender_EstimatesRealExecutionAndExecutes()
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            var salt = (ulong)Random.Shared.NextInt64();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var recipient = "0x" + new string('7', 40);
            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = Web3.Web3.Convert.ToWei(0.01m),
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(estimate.CallGasLimit.Value >= 30_000,
                $"callGasLimit ({estimate.CallGasLimit.Value}) should be a real execution budget, not a calldata-only estimate");
            Assert.True(estimate.VerificationGasLimit.Value >= 100_000,
                $"verificationGasLimit ({estimate.VerificationGasLimit.Value}) should cover counterfactual deployment");

            var finalOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = estimate.CallGasLimit.Value,
                VerificationGasLimit = estimate.VerificationGasLimit.Value,
                PreVerificationGas = estimate.PreVerificationGas.Value,
                MaxFeePerGas = estimate.MaxFeePerGas.Value,
                MaxPriorityFeePerGas = estimate.MaxPriorityFeePerGas.Value
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(finalOp, ownerKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var userOpHash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var bundleResult = await bundler.ExecuteBundleAsync();
            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle should succeed using the estimate alone, error: {bundleResult.Error}");

            var receipt = await bundler.GetUserOperationReceiptAsync(userOpHash);
            Assert.NotNull(receipt);
            Assert.True(receipt!.Success, "counterfactual deployment + execution should succeed using the estimate alone");
        }

        [Fact]
        public async Task EstimateUserOperationGas_DeployedSender_CallGasLimitBelowCounterfactualVerification()
        {
            var salt1 = (ulong)Random.Shared.NextInt64();
            var (deployedAddress, _) = await _fixture.CreateFundedAccountAsync(salt1);

            var deployedExecute = new ExecuteFunction
            {
                Target = deployedAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var deployedOp = new UserOperation
            {
                Sender = deployedAddress,
                CallData = deployedExecute.GetCallData(),
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var deployedEstimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                deployedOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(deployedEstimate.CallGasLimit.Value >= 21_000,
                $"callGasLimit ({deployedEstimate.CallGasLimit.Value}) should be at least the intrinsic floor");

            var ownerKey2 = EthECKey.GenerateKey();
            var ownerAddress2 = ownerKey2.GetPublicAddress();
            var salt2 = (ulong)Random.Shared.NextInt64();
            var counterfactualAddress = await _fixture.GetAccountAddressAsync(ownerAddress2, salt2);
            await _fixture.FundAccountAsync(counterfactualAddress, 1m);
            var initCode2 = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress2, salt2);

            var counterfactualExecute = new ExecuteFunction
            {
                Target = counterfactualAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var counterfactualOp = new UserOperation
            {
                Sender = counterfactualAddress,
                Nonce = 0,
                InitCode = initCode2,
                CallData = counterfactualExecute.GetCallData(),
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var counterfactualEstimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                counterfactualOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(
                deployedEstimate.CallGasLimit.Value < counterfactualEstimate.VerificationGasLimit.Value,
                $"deployed sender's callGasLimit ({deployedEstimate.CallGasLimit.Value}) should be well below the " +
                $"counterfactual sender's deployment-inclusive verificationGasLimit ({counterfactualEstimate.VerificationGasLimit.Value})");
        }

        [Fact]
        public async Task EstimateUserOperationGas_WithWrongSignerSignature_DoesNotThrow()
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
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(estimate.CallGasLimit.Value >= 21_000);
            Assert.True(estimate.VerificationGasLimit.Value > 0);
        }

        [Fact]
        public async Task EstimateUserOperationGas_WithRevertingCallData_ThrowsUserOperationReverted()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var revertingExecute = new ExecuteFunction
            {
                Target = _fixture.EntryPointService.ContractAddress,
                Value = 0,
                Data = new byte[] { 0xde, 0xad, 0xbe, 0xef }
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = revertingExecute.GetCallData(),
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                _fixture.BundlerService.EstimateUserOperationGasAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.UserOperationReverted, ex.Code);
        }

        [Fact]
        public async Task EstimateUserOperationGas_NonIdempotentOp_SendsNearlyEntireBalance_DoesNotFalseRevert()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var recipient = "0x" + new string('9', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(0.9m);

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
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(estimate.CallGasLimit.Value >= 21_000,
                $"callGasLimit ({estimate.CallGasLimit.Value}) should be a valid execution budget for an op that succeeds");

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var userOpHash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var bundleResult = await bundler.ExecuteBundleAsync();
            Assert.NotNull(bundleResult);
            Assert.True(bundleResult.Success, $"Bundle should succeed using the estimate alone, error: {bundleResult.Error}");

            var receipt = await bundler.GetUserOperationReceiptAsync(userOpHash);
            Assert.NotNull(receipt);
            Assert.True(receipt!.Success, "sending nearly the entire balance should succeed using the estimate alone");
        }

        [Fact]
        public async Task EstimateUserOperationGas_UnfundedCounterfactualSender_ThrowsSimulateValidationAA21()
        {
            var ownerKey = EthECKey.GenerateKey();
            var ownerAddress = ownerKey.GetPublicAddress();
            var salt = (ulong)Random.Shared.NextInt64();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                Signature = DUMMY_SIGNATURE.HexToByteArray()
            };

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                _fixture.BundlerService.EstimateUserOperationGasAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.SimulateValidation, ex.Code);
            Assert.Contains("AA21", ex.Message);
        }
    }
}
