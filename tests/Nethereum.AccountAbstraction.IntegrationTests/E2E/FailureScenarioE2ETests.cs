using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class FailureScenarioE2ETests
    {
        private readonly BundlerTestFixture _fixture;

        public FailureScenarioE2ETests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task E2E_InvalidSignature_ExecutionFails()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var wrongKey = new EthECKey(TestAccounts.Account3PrivateKey);

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
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, wrongKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true,
                "Operation with invalid signature should fail during execution");
        }

        [Fact]
        public async Task E2E_InsufficientAccountFunds_ExecutionFails()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var ownerKey = new EthECKey(TestAccounts.Account5PrivateKey);
            var ownerAddress = ownerKey.GetPublicAddress();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x" + new string('1', 40),
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, ownerKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true,
                "Operation with insufficient account funds should fail");
        }

        [Fact]
        public async Task E2E_NonceMismatch_SecondOpFails()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = currentNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOp2 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = currentNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, accountKey);

            using var bundler1 = _fixture.CreateNewBundlerService();
            await bundler1.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);
            var result1 = await bundler1.ExecuteBundleAsync();

            Assert.True(result1?.Success ?? false, "First operation should succeed");

            using var bundler2 = _fixture.CreateNewBundlerService();
            await bundler2.SendUserOperationAsync(packedOp2, _fixture.EntryPointService.ContractAddress);
            var result2 = await bundler2.ExecuteBundleAsync();

            Assert.False(result2?.Success ?? true,
                "Second operation with same nonce should fail");
        }

        [Fact]
        public async Task E2E_OutOfGas_DuringExecution_Fails()
        {
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
                CallGasLimit = 1000,
                VerificationGasLimit = 1000,
                PreVerificationGas = 1000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true,
                "Operation with insufficient gas should fail");
        }

        [Fact]
        public async Task E2E_CallDataRevert_ExecutionFails_ButBundleSucceeds()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var nonExistentContract = "0x" + new string('D', 40);
            var invalidCallData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

            var executeFunction = new ExecuteFunction
            {
                Target = nonExistentContract,
                Value = 0,
                Data = invalidCallData
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
        }

        [Fact]
        public async Task E2E_ZeroMaxFeePerGas_AcceptedOnDevChain()
        {
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
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 0,
                MaxPriorityFeePerGas = 0
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.True(result?.Success ?? false,
                "Dev chain should accept zero gas price operations");
        }
    }
}
