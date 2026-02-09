using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
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
    public class PaymasterE2ETests
    {
        private readonly BundlerTestFixture _fixture;

        public PaymasterE2ETests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_FullFlow_NoAccountFunding()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var salt = (ulong)Random.Shared.NextInt64();
            var ownerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var ownerAddress = ownerKey.GetPublicAddress();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var recipient = "0x" + new string('8', 40);

            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var estimate = await _fixture.BundlerService.EstimateUserOperationGasAsync(
                userOp, _fixture.EntryPointService.ContractAddress);

            userOp.VerificationGasLimit = (long)(estimate.VerificationGasLimit.Value * 15 / 10);
            userOp.PreVerificationGas = (long)estimate.PreVerificationGas.Value;
            userOp.CallGasLimit = (long)(estimate.CallGasLimit.Value * 12 / 10);

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, ownerKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.False(string.IsNullOrEmpty(hash));

            var result = await bundler.ExecuteBundleAsync();
            Assert.NotNull(result);
            Assert.True(result.Success, $"Paymaster-sponsored operation failed: {result.Error}");

            var code = await _fixture.Web3.Eth.GetCode.SendRequestAsync(accountAddress);
            Assert.NotEqual("0x", code);
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_ERC20Transfer_UserPaysNoGas()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Paymaster Test Token",
                TokenSymbol = "PMT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = accountAddress, Value = Web3.Web3.Convert.ToWei(1000) });

            var ethBalanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);

            var recipient = "0x" + new string('9', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(100);

            var transfer = new TransferFunction { To = recipient, Value = transferAmount };
            var execute = new ExecuteFunction
            {
                Target = tokenService.ContractHandler.ContractAddress,
                Value = 0,
                Data = transfer.GetCallData()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = execute.GetCallData(),
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 50_000,
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
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Paymaster ERC20 transfer failed: {result.Error}");

            var recipientBalance = await tokenService.BalanceOfQueryAsync(recipient);
            Assert.Equal(transferAmount, recipientBalance);

            var ethBalanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(ethBalanceBefore.Value, ethBalanceAfter.Value);
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_BatchTransfer_MultipleRecipients()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(10m);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Batch Paymaster Token",
                TokenSymbol = "BPT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = accountAddress, Value = Web3.Web3.Convert.ToWei(10000) });

            var recipients = new[]
            {
                "0x" + new string('A', 40),
                "0x" + new string('B', 40),
                "0x" + new string('C', 40),
                "0x" + new string('D', 40),
                "0x" + new string('E', 40)
            };

            var transferAmount = Web3.Web3.Convert.ToWei(100);
            var tokenAddress = tokenService.ContractHandler.ContractAddress;

            var batchCalls = recipients.Select(r => new Call
            {
                Target = tokenAddress,
                Value = 0,
                Data = new TransferFunction { To = r, Value = transferAmount }.GetCallData()
            }).ToList();

            var batchExecute = new ExecuteBatchFunction { Calls = batchCalls };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = batchExecute.GetCallData(),
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 50_000,
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
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Paymaster batch transfer failed: {result.Error}");

            foreach (var recipient in recipients)
            {
                var balance = await tokenService.BalanceOfQueryAsync(recipient);
                Assert.Equal(transferAmount, balance);
            }
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_MultipleSequentialOps()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(10m);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var tokenDeployment = new EIP20Deployment
            {
                InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                TokenName = "Sequential Paymaster Token",
                TokenSymbol = "SPT",
                DecimalUnits = 18
            };

            var tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, tokenDeployment);

            await tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = accountAddress, Value = Web3.Web3.Convert.ToWei(1000) });

            var tokenAddress = tokenService.ContractHandler.ContractAddress;
            var ethBalanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);

            var hexChars = new[] { '1', '2', '3' };
            for (int i = 0; i < 3; i++)
            {
                var recipient = "0x" + new string(hexChars[i], 40);
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
                    Paymaster = paymasterService.ContractHandler.ContractAddress,
                    PaymasterData = Array.Empty<byte>(),
                    PaymasterVerificationGasLimit = 100_000,
                    PaymasterPostOpGasLimit = 50_000,
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
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await bundler.ExecuteBundleAsync();

                Assert.True(result?.Success ?? false, $"Operation {i + 1} failed: {result?.Error}");

                var recipientBalance = await tokenService.BalanceOfQueryAsync(recipient);
                Assert.Equal(transferAmount, recipientBalance);
            }

            var ethBalanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(ethBalanceBefore.Value, ethBalanceAfter.Value);
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_InsufficientDeposit_Fails()
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

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
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                VerificationGasLimit = 200_000,
                CallGasLimit = 100_000,
                PreVerificationGas = 50_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true,
                "Operation with insufficient paymaster deposit should fail during execution");
        }

        [Fact]
        public async Task E2E_PaymasterDeposit_BalanceTracking()
        {
            var paymasterService = await DeployPaymasterAsync();
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var depositBefore = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            Assert.Equal(BigInteger.Zero, depositBefore);

            var depositAmount = Web3.Web3.Convert.ToWei(1);
            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.DepositToFunction
                {
                    Account = paymasterAddress,
                    AmountToSend = depositAmount
                });

            var depositAfter = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            Assert.Equal(depositAmount, depositAfter);
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_GasAccountingValidation()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(10m);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var paymasterDepositBefore = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

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
                Paymaster = paymasterAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 50_000,
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
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.GasUsed > 0);

            var paymasterDepositAfter = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            Assert.True(paymasterDepositAfter < paymasterDepositBefore,
                "Paymaster deposit should decrease after sponsoring an operation");
        }

        [Fact]
        public async Task E2E_PaymasterSponsorship_WithPaymasterData()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var customPaymasterData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Paymaster = paymasterService.ContractHandler.ContractAddress,
                PaymasterData = customPaymasterData,
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 50_000,
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
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Operation with paymaster data failed: {result.Error}");
        }

        private async Task<TestPaymasterAcceptAllService> DeployPaymasterAsync()
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress
            };

            return await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);
        }

        private async Task<TestPaymasterAcceptAllService> DeployAndFundPaymasterAsync(decimal ethAmount)
        {
            var paymasterService = await DeployPaymasterAsync();
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

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
    }
}
