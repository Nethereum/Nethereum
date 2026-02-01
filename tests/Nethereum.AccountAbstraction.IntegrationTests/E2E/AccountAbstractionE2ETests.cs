using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.StandardTokenEIP20;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class AccountAbstractionE2ETests
    {
        private readonly BundlerTestFixture _fixture;

        public AccountAbstractionE2ETests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task E2E_CreateAccount_DeployERC20_TransferTokens_FullWorkflow()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 2m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Test Token",
                TokenSymbol = "TEST",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            var tokenAddress = tokenService.ContractHandler.ContractAddress;
            Assert.NotEmpty(tokenAddress);

            var transferToAccount = new TransferFunction
            {
                To = accountAddress,
                Value = Web3.Web3.Convert.ToWei(10_000)
            };
            var transferReceipt = await tokenService.TransferRequestAndWaitForReceiptAsync(transferToAccount);
            Assert.NotNull(transferReceipt);
            Assert.True(transferReceipt.Status.Value == 1, "Token transfer to account failed");

            var accountBalance = await tokenService.BalanceOfQueryAsync(accountAddress);
            Assert.True(accountBalance > 0, $"Account balance should be positive after transfer. Token address: {tokenAddress}");
            Assert.Equal(Web3.Web3.Convert.ToWei(10_000), accountBalance);

            var recipient = "0x" + new string('5', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(100);

            var transferFromAccount = new TransferFunction
            {
                To = recipient,
                Value = transferAmount
            };

            var executeFunction = new ExecuteFunction
            {
                Target = tokenAddress,
                Value = 0,
                Data = transferFromAccount.GetCallData()
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

            Assert.True(estimate.PreVerificationGas.Value > 0);
            Assert.True(estimate.VerificationGasLimit.Value > 0);
            Assert.True(estimate.CallGasLimit.Value > 0);

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(hash));

            var result = await bundler.ExecuteBundleAsync();
            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle failed: {result.Error}");

            var recipientBalance = await tokenService.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var accountBalanceAfter = await tokenService.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(10_000) - transferAmount, accountBalanceAfter);
        }

        [Fact]
        public async Task E2E_BatchTransfer_MultipleERC20Recipients()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 2m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Batch Test Token",
                TokenSymbol = "BTT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            var tokenAddress = tokenService.ContractHandler.ContractAddress;

            var transferToAccount = new TransferFunction
            {
                To = accountAddress,
                Value = Web3.Web3.Convert.ToWei(10_000)
            };
            await tokenService.TransferRequestAndWaitForReceiptAsync(transferToAccount);

            var recipient1 = "0x" + new string('1', 40);
            var recipient2 = "0x" + new string('2', 40);
            var recipient3 = "0x" + new string('3', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(100);

            var transfer1 = new TransferFunction { To = recipient1, Value = transferAmount };
            var transfer2 = new TransferFunction { To = recipient2, Value = transferAmount };
            var transfer3 = new TransferFunction { To = recipient3, Value = transferAmount };

            var batchCalls = new List<Call>
            {
                new Call { Target = tokenAddress, Value = 0, Data = transfer1.GetCallData() },
                new Call { Target = tokenAddress, Value = 0, Data = transfer2.GetCallData() },
                new Call { Target = tokenAddress, Value = 0, Data = transfer3.GetCallData() }
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
            Assert.True(result.Success, $"Batch transfer failed: {result.Error}");

            var balance1 = await tokenService.BalanceOfQueryAsync(recipient1);
            var balance2 = await tokenService.BalanceOfQueryAsync(recipient2);
            var balance3 = await tokenService.BalanceOfQueryAsync(recipient3);

            Assert.Equal(transferAmount, balance1);
            Assert.Equal(transferAmount, balance2);
            Assert.Equal(transferAmount, balance3);
        }

        [Fact]
        public async Task E2E_NewAccountCreation_WithInitCode_DeploysAndExecutes()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var newOwnerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var newOwnerAddress = newOwnerKey.GetPublicAddress();

            var newAccountAddress = await _fixture.GetAccountAddressAsync(newOwnerAddress, salt);
            await _fixture.FundAccountAsync(newAccountAddress, 1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(newOwnerAddress, salt);

            var recipient = "0x" + new string('6', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(0.01m);

            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = transferAmount,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = newAccountAddress,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.True(estimate.VerificationGasLimit.Value >= GasEstimationConstants.CREATE2_COST,
                "Verification gas should include account deployment cost");

            userOp.VerificationGasLimit = (long)(estimate.VerificationGasLimit.Value * 15 / 10);
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)(estimate.CallGasLimit.Value * 12 / 10);

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, newOwnerKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();
            Assert.NotNull(result);
            Assert.True(result.Success, $"Account creation failed: {result.Error}");

            var code = await _fixture.Web3.Eth.GetCode.SendRequestAsync(newAccountAddress);
            Assert.NotEqual("0x", code);
            Assert.True(code.Length > 2, "Account should have code deployed");

            var recipientBalance = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(recipient);
            Assert.True(recipientBalance.Value >= transferAmount,
                $"Recipient should have received {transferAmount} wei");
        }

        [Fact]
        public async Task E2E_GasEstimation_AccurateForComplexOperations()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 2m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Gas Test Token",
                TokenSymbol = "GTT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            var tokenAddress = tokenService.ContractHandler.ContractAddress;

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = accountAddress, Value = Web3.Web3.Convert.ToWei(1000) });

            var recipients = Enumerable.Range(1, 5)
                .Select(i => "0x" + new string((char)('0' + i), 40))
                .ToArray();

            var batchCalls = recipients.Select(r => new Call
            {
                Target = tokenAddress,
                Value = 0,
                Data = new TransferFunction { To = r, Value = Web3.Web3.Convert.ToWei(10) }.GetCallData()
            }).ToList();

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

            var callDataCost = UserOperationGasEstimator.CalculateCalldataCost(batchExecute.GetCallData());
            Assert.True(estimate.PreVerificationGas.Value >=
                GasEstimationConstants.PRE_VERIFICATION_OVERHEAD_GAS + (long)callDataCost / 2,
                "PreVerificationGas should reflect calldata size");

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();
            Assert.NotNull(result);
            Assert.True(result.Success, $"Complex operation failed: {result.Error}");

            var totalEstimated = estimate.PreVerificationGas.Value +
                                estimate.VerificationGasLimit.Value +
                                estimate.CallGasLimit.Value;

            Assert.True(result.GasUsed > 0, "Should have used gas");
            Assert.True(result.GasUsed <= totalEstimated,
                $"Actual gas ({result.GasUsed}) should not exceed estimate ({totalEstimated})");
        }

        [Fact]
        public async Task E2E_MultipleSequentialOperations_NonceManagement()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 2m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Nonce Test Token",
                TokenSymbol = "NTT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = accountAddress, Value = Web3.Web3.Convert.ToWei(1000) });

            var tokenAddress = tokenService.ContractHandler.ContractAddress;

            for (int i = 0; i < 3; i++)
            {
                var recipient = "0x" + new string((char)('A' + i), 40);
                var transferAmount = Web3.Web3.Convert.ToWei(10);

                var transfer = new TransferFunction { To = recipient, Value = transferAmount };
                var execute = new ExecuteFunction
                {
                    Target = tokenAddress,
                    Value = 0,
                    Data = transfer.GetCallData()
                };

                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = execute.GetCallData(),
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                };

                var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                    userOp, _fixture.EntryPointService.ContractAddress);

                userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
                userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
                userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

                using var bundler = _fixture.CreateNewBundlerService();
                var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);

                var result = await bundler.ExecuteBundleAsync();
                Assert.NotNull(result);
                Assert.True(result.Success, $"Operation {i + 1} failed: {result.Error}");

                var recipientBalance = await tokenService.BalanceOfQueryAsync(recipient);
                Assert.Equal(transferAmount, recipientBalance);
            }

            var finalAccountBalance = await tokenService.BalanceOfQueryAsync(accountAddress);
            Assert.Equal(Web3.Web3.Convert.ToWei(1000 - 30), finalAccountBalance);
        }

        [Fact]
        public async Task E2E_ApproveAndTransferFrom_TwoStepTokenOperation()
        {
            var salt1 = (ulong)Random.Shared.NextInt64();
            var salt2 = (ulong)Random.Shared.NextInt64();

            var (ownerAccount, ownerKey) = await _fixture.CreateFundedAccountAsync(salt1, 2m);
            var (spenderAccount, spenderKey) = await _fixture.CreateFundedAccountAsync(salt2, 2m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Approve Test Token",
                TokenSymbol = "ATT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            var tokenAddress = tokenService.ContractHandler.ContractAddress;

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = ownerAccount, Value = Web3.Web3.Convert.ToWei(1000) });

            var approveAmount = Web3.Web3.Convert.ToWei(500);
            var approve = new ApproveFunction { Spender = spenderAccount, Value = approveAmount };
            var approveExecute = new ExecuteFunction
            {
                Target = tokenAddress,
                Value = 0,
                Data = approve.GetCallData()
            };

            var approveOp = new UserOperation
            {
                Sender = ownerAccount,
                CallData = approveExecute.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var approveEstimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                approveOp, _fixture.EntryPointService.ContractAddress);

            approveOp.VerificationGasLimit = (long)approveEstimate.VerificationGasLimit.Value;
            approveOp.PreVerificationGas = (long)approveEstimate.PreVerificationGas.Value;
            approveOp.CallGasLimit = (long)approveEstimate.CallGasLimit.Value;

            var packedApproveOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(approveOp, ownerKey);

            using (var bundler = _fixture.CreateNewBundlerService())
            {
                await bundler.SendUserOperationAsync(packedApproveOp, _fixture.EntryPointService.ContractAddress);
                var result = await bundler.ExecuteBundleAsync();
                Assert.True(result?.Success ?? false, $"Approve failed: {result?.Error}");
            }

            var allowance = await tokenService.AllowanceQueryAsync(ownerAccount, spenderAccount);
            Assert.Equal(approveAmount, allowance);
        }

        [Fact]
        public async Task E2E_FullValidationFlow_EstimateSignSubmitExecuteVerify()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 2m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Validation Test Token",
                TokenSymbol = "VTT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = accountAddress, Value = Web3.Web3.Convert.ToWei(1000) });

            var recipient = "0x" + new string('7', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(50);
            var tokenAddress = tokenService.ContractHandler.ContractAddress;

            var transfer = new TransferFunction { To = recipient, Value = transferAmount };
            var execute = new ExecuteFunction
            {
                Target = tokenAddress,
                Value = 0,
                Data = transfer.GetCallData()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = execute.GetCallData(),
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            Assert.True(estimate.PreVerificationGas.Value >= GasEstimationConstants.PRE_VERIFICATION_OVERHEAD_GAS);
            Assert.True(estimate.VerificationGasLimit.Value > 0);
            Assert.True(estimate.CallGasLimit.Value > 0);

            userOp.VerificationGasLimit = (long)estimate.VerificationGasLimit.Value;
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)estimate.CallGasLimit.Value;

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            Assert.NotNull(packedOp.Signature);
            Assert.True(packedOp.Signature.Length > 0);

            using var bundler = _fixture.CreateNewBundlerService();

            var balanceBefore = await tokenService.BalanceOfQueryAsync(recipient);

            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(hash));

            var status = await bundler.GetUserOperationStatusAsync(hash);
            Assert.NotNull(status);
            Assert.Equal(UserOpState.Pending, status.State);

            var result = await bundler.ExecuteBundleAsync();
            Assert.NotNull(result);
            Assert.True(result.Success, $"Execution failed: {result.Error}");
            Assert.NotNull(result.TransactionHash);
            Assert.True(result.GasUsed > 0);

            var receipt = await bundler.GetUserOperationReceiptAsync(hash);
            Assert.NotNull(receipt);
            Assert.True(receipt.Success);

            var balanceAfter = await tokenService.BalanceOfQueryAsync(recipient);
            Assert.Equal(balanceBefore + transferAmount, balanceAfter);
        }
    }
}
