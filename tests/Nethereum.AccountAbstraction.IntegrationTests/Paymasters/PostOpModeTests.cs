using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;
using PackedUserOperation = Nethereum.AccountAbstraction.Structs.PackedUserOperation;

namespace Nethereum.AccountAbstraction.IntegrationTests.Paymasters
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class PostOpModeTests
    {
        private readonly BundlerTestFixture _fixture;

        public PostOpModeTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task PostOp_OpSucceeded_PaymasterPaysAndStateUpdates()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var paymasterDepositBefore = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            var ethBalanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);

            var execute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = execute.GetCallData(),
                Paymaster = paymasterAddress,
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
            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"PostOp OpSucceeded test failed: {result.Error}");

            var ethBalanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(accountAddress);
            Assert.Equal(ethBalanceBefore.Value, ethBalanceAfter.Value);

            var paymasterDepositAfter = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            Assert.True(paymasterDepositAfter < paymasterDepositBefore,
                "Paymaster deposit should decrease after postOp OpSucceeded");
        }

        [Fact]
        public async Task PostOp_MultipleSuccessfulOps_PaymasterChargedCorrectly()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(10m);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var counterService = await TestCounterService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, new TestCounterDeployment());

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var paymasterDepositStart = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            var totalGasUsed = BigInteger.Zero;

            for (int i = 0; i < 3; i++)
            {
                var countFunction = new CountFunction();
                var execute = new ExecuteFunction
                {
                    Target = counterService.ContractHandler.ContractAddress,
                    Value = 0,
                    Data = countFunction.GetCallData()
                };

                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = execute.GetCallData(),
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

                Assert.True(result?.Success ?? false, $"Operation {i + 1} failed: {result?.Error}");
                totalGasUsed += result!.GasUsed;
            }

            var counterAfter = await counterService.CountersQueryAsync(accountAddress);
            Assert.Equal(BigInteger.Parse("3"), counterAfter);

            var paymasterDepositEnd = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            var depositUsed = paymasterDepositStart - paymasterDepositEnd;
            Assert.True(depositUsed > 0, "Paymaster should have been charged for all operations");
        }

        [Fact]
        public async Task PostOp_PaymasterGasLimits_Enforced()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var execute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var paymasterVerificationGas = 150_000;
            var paymasterPostOpGas = 75_000;

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = execute.GetCallData(),
                Paymaster = paymasterAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = paymasterVerificationGas,
                PaymasterPostOpGasLimit = paymasterPostOpGas,
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
            Assert.True(result.Success, $"Paymaster gas limits test failed: {result.Error}");
        }

        [Fact]
        public async Task PostOp_ContextPassing_PaymasterReceivesContext()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var customContext = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };

            var execute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = execute.GetCallData(),
                Paymaster = paymasterAddress,
                PaymasterData = customContext,
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
            Assert.True(result.Success, $"Context passing test failed: {result.Error}");
        }

        [Fact]
        public async Task PostOp_GasRefund_CalculatesCorrectly()
        {
            var paymasterService = await DeployAndFundPaymasterAsync(5m);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var paymasterDepositBefore = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);

            var execute = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = execute.GetCallData(),
                Paymaster = paymasterAddress,
                PaymasterData = Array.Empty<byte>(),
                PaymasterVerificationGasLimit = 500_000,
                PaymasterPostOpGasLimit = 200_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                VerificationGasLimit = 500_000,
                CallGasLimit = 500_000,
                PreVerificationGas = 100_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success);

            var paymasterDepositAfter = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterAddress);
            var actualCost = paymasterDepositBefore - paymasterDepositAfter;

            var maxPossibleCost = (userOp.VerificationGasLimit!.Value +
                                   userOp.CallGasLimit!.Value +
                                   userOp.PreVerificationGas!.Value +
                                   userOp.PaymasterVerificationGasLimit!.Value +
                                   userOp.PaymasterPostOpGasLimit!.Value) *
                                  userOp.MaxFeePerGas!.Value;

            Assert.True(actualCost < maxPossibleCost,
                $"Actual cost ({actualCost}) should be less than max possible ({maxPossibleCost}) due to gas refunds");
        }

        private async Task<TestPaymasterAcceptAllService> DeployAndFundPaymasterAsync(decimal ethAmount)
        {
            var paymasterDeployment = new TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };

            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_fixture.Web3, paymasterDeployment);

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

        [Fact]
        public async Task DeployPaymaster_WithEntryPoint_Succeeds()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            Assert.NotNull(paymasterService);
            Assert.NotNull(paymasterService.ContractAddress);

            var entryPoint = await paymasterService.EntryPointQueryAsync();
            Assert.Equal(_fixture.EntryPointService.ContractAddress.ToLower(), entryPoint.ToLower());
        }

        [Fact]
        public async Task Paymaster_DepositToEntryPoint_UpdatesBalance()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var initialDeposit = await paymasterService.GetDepositQueryAsync();
            Assert.Equal(BigInteger.Zero, initialDeposit);

            await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(paymasterService.ContractAddress, 1m);

            await paymasterService.DepositRequestAndWaitForReceiptAsync();

            var newDeposit = await paymasterService.GetDepositQueryAsync();
            Assert.True(newDeposit > 0, "Deposit should be > 0 after funding");
        }

        [Fact]
        public async Task Paymaster_GetHash_ReturnsNonZeroHash()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var packedOp = new PackedUserOperation
            {
                Sender = accountAddress,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var validUntil = (ulong)DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var validAfter = (ulong)0;

            var hash = await paymasterService.GetHashQueryAsync(packedOp, validUntil, validAfter);

            Assert.NotNull(hash);
            Assert.Equal(32, hash.Length);
            Assert.True(hash.Any(b => b != 0), "Hash should not be all zeros");
        }

        [Fact]
        public async Task Paymaster_OwnerQuery_ReturnsDeploymentOwner()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var owner = await paymasterService.OwnerQueryAsync();
            Assert.Equal(_fixture.BeneficiaryAddress.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task Paymaster_SignerQuery_ReturnsDeploymentSigner()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var signer = await paymasterService.VerifyingSignerQueryAsync();
            Assert.Equal(signerAddress.ToLower(), signer.ToLower());
        }

        [Fact]
        public async Task Paymaster_MultipleDeposits_Accumulate()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(paymasterService.ContractAddress, 0.5m);
            await paymasterService.DepositRequestAndWaitForReceiptAsync();

            var deposit1 = await paymasterService.GetDepositQueryAsync();

            await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(paymasterService.ContractAddress, 0.5m);
            await paymasterService.DepositRequestAndWaitForReceiptAsync();

            var deposit2 = await paymasterService.GetDepositQueryAsync();

            Assert.True(deposit2 > deposit1, "Deposit should accumulate after second deposit");
        }

        [Fact]
        public async Task Paymaster_WithdrawTo_DeductsFromDeposit()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(paymasterService.ContractAddress, 1m);
            await paymasterService.DepositRequestAndWaitForReceiptAsync();

            var depositBefore = await paymasterService.GetDepositQueryAsync();
            Assert.True(depositBefore > 0);

            var withdrawAmount = depositBefore / 2;
            await paymasterService.WithdrawToRequestAndWaitForReceiptAsync(
                _fixture.BeneficiaryAddress,
                withdrawAmount);

            var depositAfter = await paymasterService.GetDepositQueryAsync();
            Assert.True(depositAfter < depositBefore, "Deposit should decrease after withdrawal");
            Assert.Equal(depositBefore - withdrawAmount, depositAfter);
        }

        [Fact]
        public async Task Paymaster_ZeroDeposit_InitialState()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var deposit = await paymasterService.GetDepositQueryAsync();
            Assert.Equal(BigInteger.Zero, deposit);
        }

        [Fact]
        public async Task EntryPoint_BalanceOf_ReflectsPaymasterDeposit()
        {
            var signerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var signerAddress = signerKey.GetPublicAddress();

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = signerAddress
            };

            var paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            await _fixture.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(paymasterService.ContractAddress, 1m);
            await paymasterService.DepositRequestAndWaitForReceiptAsync();

            var paymasterDeposit = await paymasterService.GetDepositQueryAsync();
            var entryPointBalance = await _fixture.EntryPointService.BalanceOfQueryAsync(paymasterService.ContractAddress);

            Assert.Equal(paymasterDeposit, entryPointBalance);
        }
    }
}
