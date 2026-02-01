using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.UserOperations
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class UserOperationHashingTests
    {
        private readonly BundlerTestFixture _fixture;

        public UserOperationHashingTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetUserOpHash_ReturnsValidHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var hash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);

            Assert.NotNull(hash);
            Assert.Equal(32, hash.Length);
            Assert.True(hash.Any(b => b != 0), "Hash should not be all zeros");
        }

        [Fact]
        public async Task GetUserOpHash_SameInput_ProducesConsistentHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);

            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public async Task GetUserOpHash_DifferentSenders_ProducesDifferentHashes()
        {
            var salt1 = (ulong)Random.Shared.NextInt64();
            var salt2 = (ulong)Random.Shared.NextInt64();
            var (accountAddress1, accountKey1) = await _fixture.CreateFundedAccountAsync(salt1);
            var (accountAddress2, accountKey2) = await _fixture.CreateFundedAccountAsync(salt2);

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress1,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey1);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress2,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey2);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp1);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp2);

            Assert.NotEqual(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task GetUserOpHash_DifferentNonces_ProducesDifferentHashes()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    Nonce = BigInteger.Zero
                },
                accountKey);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    Nonce = BigInteger.One
                },
                accountKey);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp1);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp2);

            Assert.NotEqual(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task GetUserOpHash_DifferentCallData_ProducesDifferentHashes()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction1 = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = new byte[] { 0x01 }
            };

            var executeFunction2 = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = new byte[] { 0x02 }
            };

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction1.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction2.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp1);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp2);

            Assert.NotEqual(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task GetUserOpHash_SignatureDoesNotAffectHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey1) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey1);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);

            var userOpWithEmptySignature = new Structs.PackedUserOperation
            {
                Sender = userOp.Sender,
                Nonce = userOp.Nonce,
                InitCode = userOp.InitCode,
                CallData = userOp.CallData,
                AccountGasLimits = userOp.AccountGasLimits,
                PreVerificationGas = userOp.PreVerificationGas,
                GasFees = userOp.GasFees,
                PaymasterAndData = userOp.PaymasterAndData,
                Signature = new byte[65]
            };

            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOpWithEmptySignature);

            Assert.Equal(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task UserOpHash_MatchesBundlerReturnedHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000
                },
                accountKey);

            var calculatedHash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);

            using var bundler = _fixture.CreateNewBundlerService();
            var bundlerReturnedHash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);

            Assert.Equal("0x" + calculatedHash.ToHex(false), bundlerReturnedHash.ToLowerInvariant());
        }

        [Fact]
        public async Task GetUserOpHash_WithInitCode_IncludesInHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var ownerKey = new EthECKey(TestAccounts.Account5PrivateKey);
            var ownerAddress = ownerKey.GetPublicAddress();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOpWithInitCode = new Structs.PackedUserOperation
            {
                Sender = accountAddress,
                Nonce = BigInteger.Zero,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var userOpWithoutInitCode = new Structs.PackedUserOperation
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

            var hashWithInitCode = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOpWithInitCode);
            var hashWithoutInitCode = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOpWithoutInitCode);

            Assert.NotEqual(hashWithInitCode.ToHex(), hashWithoutInitCode.ToHex());
        }

        [Fact]
        public async Task GetUserOpHash_WithPaymasterData_IncludesInHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var paymasterData = new byte[20];
            new Random(42).NextBytes(paymasterData);

            var userOpWithPaymaster = new Structs.PackedUserOperation
            {
                Sender = accountAddress,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterData,
                Signature = new byte[65]
            };

            var userOpWithoutPaymaster = new Structs.PackedUserOperation
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

            var hashWithPaymaster = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOpWithPaymaster);
            var hashWithoutPaymaster = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOpWithoutPaymaster);

            Assert.NotEqual(hashWithPaymaster.ToHex(), hashWithoutPaymaster.ToHex());
        }

        [Fact]
        public async Task GetUserOpHash_DifferentGasLimits_ProducesDifferentHashes()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 200_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp1);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp2);

            Assert.NotEqual(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task GetUserOpHash_ReturnsHexFormat()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var hash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);

            var hexString = hash.ToHex();

            Assert.Equal(64, hexString.Length);
            Assert.All(hexString, c => Assert.True(
                (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'),
                "Hash should be valid hex"));
        }
    }
}
