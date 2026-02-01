using System.Numerics;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class NonceManagementE2ETests
    {
        private readonly BundlerTestFixture _fixture;

        public NonceManagementE2ETests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task E2E_2DNonce_ParallelKeys_ExecuteIndependently()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            BigInteger key0 = 100;
            BigInteger key1 = 101;

            var nonce0 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key0);
            var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key1);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp0 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = nonce0,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp0 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp0, accountKey);

            using var bundler0 = _fixture.CreateNewBundlerService();
            await bundler0.SendUserOperationAsync(packedOp0, _fixture.EntryPointService.ContractAddress);
            var result0 = await bundler0.ExecuteBundleAsync();

            Assert.True(result0?.Success ?? false, "Operation with key 100 should succeed");

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = nonce1,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);

            using var bundler1 = _fixture.CreateNewBundlerService();
            await bundler1.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);
            var result1 = await bundler1.ExecuteBundleAsync();

            Assert.True(result1?.Success ?? false, "Operation with key 101 should succeed independently");

            var newNonce0 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key0);
            var newNonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key1);

            Assert.Equal(nonce0 + 1, newNonce0);
            Assert.Equal(nonce1 + 1, newNonce1);
        }

        [Fact]
        public async Task E2E_2DNonce_SequentialWithinKey_MustBeInOrder()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            BigInteger key = 200;

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            for (int i = 0; i < 3; i++)
            {
                var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);

                var userOp = new UserOperation
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

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

                using var bundler = _fixture.CreateNewBundlerService();
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await bundler.ExecuteBundleAsync();

                Assert.True(result?.Success ?? false, $"Operation {i} should succeed");

                var newNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);
                Assert.Equal(currentNonce + 1, newNonce);
            }
        }

        [Fact]
        public async Task E2E_2DNonce_OutOfOrderInSameKey_SecondFails()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            BigInteger key = 300;
            var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var futureNonce = currentNonce + 5;

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = futureNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true,
                "Operation with future nonce should fail");
        }

        [Fact]
        public async Task E2E_2DNonce_GetNonceForKey_ReturnsCorrectSequence()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            BigInteger key = 400;

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);

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
                Nonce = nonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();

            Assert.True(result?.Success ?? false, "Operation should succeed");

            var newNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);
            Assert.Equal(nonce + 1, newNonce);
        }

        [Fact]
        public async Task E2E_2DNonce_DifferentKeysCanHaveSameSequence()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            BigInteger[] keys = [500, 501, 502];

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            foreach (var key in keys)
            {
                var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);

                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    Nonce = nonce,
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                };

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

                using var bundler = _fixture.CreateNewBundlerService();
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                var result = await bundler.ExecuteBundleAsync();

                Assert.True(result?.Success ?? false, $"Operation with key {key} should succeed");
            }

            foreach (var key in keys)
            {
                var newNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key);
                var expectedNonce = (key << 64) | 1;
                Assert.Equal(expectedNonce, newNonce);
            }
        }
    }
}
