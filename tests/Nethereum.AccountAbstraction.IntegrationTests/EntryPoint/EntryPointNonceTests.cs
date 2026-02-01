using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.EntryPoint
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class EntryPointNonceTests
    {
        private readonly BundlerTestFixture _fixture;

        public EntryPointNonceTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetNonce_CounterfactualAccount_ReturnsZero()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var ownerAddress = _fixture.OperatorKey.GetPublicAddress();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);

            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            Assert.Equal(BigInteger.Zero, nonce);
        }

        [Fact]
        public async Task GetNonce_DifferentKeys_AllStartAtSequenceZero()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var ownerAddress = _fixture.OperatorKey.GetPublicAddress();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);

            var nonceKey0 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            var nonceKey1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.One);
            var nonceKey42 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, new BigInteger(42));

            var (key0, seq0) = DecodeNonce(nonceKey0);
            var (key1, seq1) = DecodeNonce(nonceKey1);
            var (key42, seq42) = DecodeNonce(nonceKey42);

            Assert.Equal(BigInteger.Zero, key0);
            Assert.Equal(BigInteger.One, key1);
            Assert.Equal(new BigInteger(42), key42);

            Assert.Equal(BigInteger.Zero, seq0);
            Assert.Equal(BigInteger.Zero, seq1);
            Assert.Equal(BigInteger.Zero, seq42);
        }

        [Fact]
        public void Nonce_EncodesKeyAndSequence_InSingleValue()
        {
            var key = new BigInteger(5);
            var sequence = new BigInteger(10);

            var encodedNonce = EncodeNonce(key, sequence);

            var (decodedKey, decodedSequence) = DecodeNonce(encodedNonce);
            Assert.Equal(key, decodedKey);
            Assert.Equal(sequence, decodedSequence);
        }

        [Fact]
        public void Nonce_Encoding_HandlesLargeKeys()
        {
            var key = new BigInteger(12345678901234567890UL);
            var sequence = new BigInteger(42);

            var encodedNonce = EncodeNonce(key, sequence);
            var (decodedKey, decodedSequence) = DecodeNonce(encodedNonce);

            Assert.Equal(key, decodedKey);
            Assert.Equal(sequence, decodedSequence);
        }

        [Fact]
        public async Task Nonce_IncrementsAfterSuccessfulUserOp()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var initialNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

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
                    VerificationGasLimit = 200_000,
                    Nonce = initialNonce
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            await bundler.FlushAsync();

            var newNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(initialNonce + 1, newNonce);
        }

        [Fact]
        public async Task Nonce_DifferentKeys_IncrementIndependently()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var key0 = BigInteger.Zero;
            var key1 = BigInteger.One;

            var nonceKey0Before = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key0);
            var nonceKey1Before = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key1);

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
                    VerificationGasLimit = 200_000,
                    Nonce = EncodeNonce(key0, GetSequence(nonceKey0Before))
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            await bundler.FlushAsync();

            var nonceKey0After = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key0);
            var nonceKey1After = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, key1);

            Assert.Equal(nonceKey0Before + 1, nonceKey0After);
            Assert.Equal(nonceKey1Before, nonceKey1After);
        }

        [Fact]
        public async Task Nonce_WrongNonce_DoesNotExecute()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            var wrongNonce = currentNonce + 10;

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
                    VerificationGasLimit = 200_000,
                    Nonce = wrongNonce
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);

            try
            {
                await bundler.FlushAsync();
            }
            catch
            {
            }

            var nonceAfterFailedOp = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(currentNonce, nonceAfterFailedOp);
        }

        [Fact]
        public async Task Nonce_DuplicateNonce_Rejected()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

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
                    Nonce = currentNonce
                },
                accountKey);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    Nonce = currentNonce
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(userOp1, _fixture.EntryPointService.ContractAddress);

            var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await bundler.SendUserOperationAsync(userOp2, _fixture.EntryPointService.ContractAddress));

            Assert.NotNull(exception);
        }

        [Fact]
        public async Task Nonce_MultipleOps_IncrementsSequentially()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var initialNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            for (int i = 0; i < 3; i++)
            {
                var currentNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

                var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                    new UserOperation
                    {
                        Sender = accountAddress,
                        CallData = executeFunction.GetCallData(),
                        CallGasLimit = 100_000,
                        VerificationGasLimit = 200_000,
                        Nonce = currentNonce
                    },
                    accountKey);

                await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
                await bundler.FlushAsync();
            }

            var finalNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(initialNonce + 3, finalNonce);
        }

        private static BigInteger EncodeNonce(BigInteger key, BigInteger sequence)
        {
            return (key << 64) | sequence;
        }

        private static (BigInteger key, BigInteger sequence) DecodeNonce(BigInteger nonce)
        {
            var key = nonce >> 64;
            var sequence = nonce & ((BigInteger.One << 64) - 1);
            return (key, sequence);
        }

        private static BigInteger GetSequence(BigInteger nonce)
        {
            return nonce & ((BigInteger.One << 64) - 1);
        }
    }
}
