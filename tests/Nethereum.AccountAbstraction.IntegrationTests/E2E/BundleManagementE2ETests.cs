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
    public class BundleManagementE2ETests
    {
        private readonly BundlerTestFixture _fixture;

        public BundleManagementE2ETests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task E2E_MixedBundle_NewAndExistingAccounts_AllExecute()
        {
            var existingSalt = (ulong)Random.Shared.NextInt64();
            var (existingAccount, existingKey) = await _fixture.CreateFundedAccountAsync(existingSalt, 0.2m);

            var newSalt = (ulong)Random.Shared.NextInt64();
            var newOwnerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var newOwnerAddress = newOwnerKey.GetPublicAddress();
            var newAccountAddress = await _fixture.GetAccountAddressAsync(newOwnerAddress, newSalt);
            await _fixture.FundAccountAsync(newAccountAddress, 0.2m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(newOwnerAddress, newSalt);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x" + new string('1', 40),
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var existingNonce = await _fixture.EntryPointService.GetNonceQueryAsync(existingAccount, BigInteger.Zero);
            var userOp1 = new UserOperation
            {
                Sender = existingAccount,
                CallData = executeFunction.GetCallData(),
                Nonce = existingNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOp2 = new UserOperation
            {
                Sender = newAccountAddress,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, existingKey);
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, newOwnerKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);
            await bundler.SendUserOperationAsync(packedOp2, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Mixed bundle should succeed: {result.Error}");

            var newCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(newAccountAddress);
            Assert.NotEqual("0x", newCode);
        }

        [Fact]
        public async Task E2E_BundleWithMultipleOps_SameSender_NoncesCorrect()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            for (int i = 0; i < 3; i++)
            {
                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    Nonce = startNonce + i,
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                };

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            }

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle with sequential nonces should succeed: {result.Error}");

            var endNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce + 3, endNonce);
        }

        [Fact]
        public async Task E2E_BundleWith5Operations_AllSucceed()
        {
            var accounts = new List<(string address, EthECKey key)>();

            for (int i = 0; i < 5; i++)
            {
                var salt = (ulong)Random.Shared.NextInt64();
                var account = await _fixture.CreateFundedAccountAsync(salt, 0.1m);
                accounts.Add(account);
            }

            var executeFunction = new ExecuteFunction
            {
                Target = "0x" + new string('2', 40),
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            foreach (var (address, key) in accounts)
            {
                var userOp = new UserOperation
                {
                    Sender = address,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                };

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, key);
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            }

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle with 5 operations should succeed: {result.Error}");
        }

        [Fact]
        public async Task E2E_EmptyBundle_HandlesGracefully()
        {
            using var bundler = _fixture.CreateNewBundlerService();

            var result = await bundler.ExecuteBundleAsync();

            Assert.True(result == null || result.Success,
                "Empty bundle should handle gracefully");
        }

        [Fact]
        public async Task E2E_BundleFlush_ExecutesAllPending()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.3m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
            var hash1 = await bundler.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);

            var statusBeforeFlush = await bundler.GetUserOperationStatusAsync(hash1);
            Assert.Equal(UserOpState.Pending, statusBeforeFlush.State);

            await bundler.FlushAsync();

            var statusAfterFlush = await bundler.GetUserOperationStatusAsync(hash1);
            Assert.True(statusAfterFlush.State == UserOpState.Included || statusAfterFlush.State == UserOpState.Failed,
                "After flush, operation should be included or failed");
        }

        [Fact]
        public async Task E2E_BundleStatusTracking_PendingToIncluded()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.2m);

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

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var pendingStatus = await bundler.GetUserOperationStatusAsync(hash);
            Assert.Equal(UserOpState.Pending, pendingStatus.State);

            var result = await bundler.ExecuteBundleAsync();
            Assert.True(result?.Success ?? false);

            var includedStatus = await bundler.GetUserOperationStatusAsync(hash);
            Assert.Equal(UserOpState.Included, includedStatus.State);

            var receipt = await bundler.GetUserOperationReceiptAsync(hash);
            Assert.NotNull(receipt);
            Assert.True(receipt.Success);
        }
    }
}
