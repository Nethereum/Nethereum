using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.UserOperations
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class UserOperationBuildingTests
    {
        private readonly BundlerTestFixture _fixture;

        public UserOperationBuildingTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task BuildUserOperation_WithMinimalParams_SetsDefaults()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>()
            };

            Assert.True(userOp.Nonce == null || userOp.Nonce == BigInteger.Zero,
                "Nonce should be null or zero when not explicitly set");
            Assert.True(userOp.InitCode == null || userOp.InitCode.Length == 0,
                "InitCode should be null or empty when not explicitly set");
        }

        [Fact]
        public async Task BuildUserOperation_WithCallData_EncodesCorrectly()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x1234567890123456789012345678901234567890",
                Value = BigInteger.Parse("1000000000000000"),
                Data = new byte[] { 0x01, 0x02, 0x03 }
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData()
            };

            Assert.NotEmpty(userOp.CallData);
            Assert.True(userOp.CallData.Length > 4);
        }

        [Fact]
        public async Task BuildUserOperation_WithGasParams_SetsCorrectly()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            Assert.Equal(100_000, userOp.CallGasLimit);
            Assert.Equal(200_000, userOp.VerificationGasLimit);
            Assert.Equal(50_000, userOp.PreVerificationGas);
            Assert.Equal(2_000_000_000, userOp.MaxFeePerGas);
            Assert.Equal(1_000_000_000, userOp.MaxPriorityFeePerGas);
        }

        [Fact]
        public async Task BuildUserOperation_WithInitCode_ForNewAccount()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var ownerAddress = _fixture.OperatorKey.GetPublicAddress();

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                InitCode = initCode,
                CallData = Array.Empty<byte>()
            };

            Assert.NotEmpty(userOp.InitCode);
            Assert.True(userOp.InitCode.Length >= 20);
        }

        [Fact]
        public async Task BuildUserOperation_WithNonce_SetsCorrectValue()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                Nonce = nonce
            };

            Assert.Equal(nonce, userOp.Nonce);
        }

        [Fact]
        public async Task PackUserOperation_EncodesGasLimitsCorrectly()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            Assert.NotNull(packedOp);
            Assert.NotNull(packedOp.AccountGasLimits);
            Assert.Equal(32, packedOp.AccountGasLimits.Length);
            Assert.NotNull(packedOp.GasFees);
            Assert.Equal(32, packedOp.GasFees.Length);
        }

        [Fact]
        public async Task PackUserOperation_PreservesCallData()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = new byte[] { 0xAA, 0xBB, 0xCC }
            };

            var callData = executeFunction.GetCallData();

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = callData,
                CallGasLimit = 100_000,
                VerificationGasLimit = 100_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            Assert.Equal(callData, packedOp.CallData);
        }

        [Fact]
        public async Task PackUserOperation_SetsSignature()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 100_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            Assert.NotNull(packedOp.Signature);
            Assert.True(packedOp.Signature.Length >= 65, "Signature should be at least 65 bytes");
        }

        [Fact]
        public async Task BuildUserOperation_MultipleOps_HaveDifferentNonces()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    Nonce = nonce1
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(userOp1, _fixture.EntryPointService.ContractAddress);
            await bundler.FlushAsync();

            var nonce2 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            Assert.Equal(nonce1 + 1, nonce2);
        }

        [Fact]
        public async Task BuildUserOperation_WithPaymaster_SetsPaymasterFields()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var paymasterAddress = "0x1234567890123456789012345678901234567890";
            var paymasterData = new byte[] { 0x01, 0x02, 0x03 };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                Paymaster = paymasterAddress,
                PaymasterData = paymasterData,
                PaymasterVerificationGasLimit = 50_000,
                PaymasterPostOpGasLimit = 30_000
            };

            Assert.Equal(paymasterAddress, userOp.Paymaster);
            Assert.Equal(paymasterData, userOp.PaymasterData);
            Assert.Equal(50_000, userOp.PaymasterVerificationGasLimit);
            Assert.Equal(30_000, userOp.PaymasterPostOpGasLimit);
        }

        [Fact]
        public async Task BuildUserOperation_WithZeroGasValues_IsValid()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 0,
                VerificationGasLimit = 0,
                PreVerificationGas = 0
            };

            Assert.Equal(0, userOp.CallGasLimit);
            Assert.Equal(0, userOp.VerificationGasLimit);
            Assert.Equal(0, userOp.PreVerificationGas);
        }

        [Fact]
        public async Task BuildUserOperation_WithMaxGasValues_HandlesLargeNumbers()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var maxGas = long.MaxValue;
            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = Array.Empty<byte>(),
                CallGasLimit = maxGas,
                VerificationGasLimit = maxGas,
                MaxFeePerGas = maxGas,
                MaxPriorityFeePerGas = maxGas
            };

            Assert.Equal(maxGas, userOp.CallGasLimit);
            Assert.Equal(maxGas, userOp.VerificationGasLimit);
        }
    }
}
