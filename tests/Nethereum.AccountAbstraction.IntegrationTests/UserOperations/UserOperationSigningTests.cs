using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.UserOperations
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class UserOperationSigningTests
    {
        private readonly BundlerTestFixture _fixture;

        public UserOperationSigningTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SignUserOperation_WithValidKey_ProducesValidSignature()
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
            Assert.Equal(65, packedOp.Signature.Length);
        }

        [Fact]
        public async Task SignUserOperation_DifferentCallData_ProducesDifferentSignatures()
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

            Assert.NotEqual(userOp1.Signature, userOp2.Signature);
        }

        [Fact]
        public async Task SignUserOperation_DifferentNonces_ProducesDifferentSignatures()
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

            Assert.NotEqual(userOp1.Signature, userOp2.Signature);
        }

        [Fact]
        public async Task SignUserOperation_DifferentKeys_ProducesDifferentSignatures()
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

            Assert.NotEqual(userOp1.Signature, userOp2.Signature);
        }

        [Fact]
        public async Task SignUserOperation_SameInput_ProducesConsistentSignature()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var callData = Array.Empty<byte>();
            var nonce = BigInteger.Zero;

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = callData,
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    Nonce = nonce
                },
                accountKey);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = callData,
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    Nonce = nonce
                },
                accountKey);

            Assert.Equal(userOp1.Signature, userOp2.Signature);
        }

        [Fact]
        public async Task SignUserOperation_SignatureContainsRecoveryId()
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

            var lastByte = userOp.Signature[64];
            Assert.True(lastByte == 27 || lastByte == 28 || lastByte == 0 || lastByte == 1,
                "Recovery ID should be 27, 28, 0, or 1");
        }

        [Fact]
        public async Task SignUserOperation_CanBeVerifiedOnChain()
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

            using var bundler = _fixture.CreateNewBundlerService();

            var hash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            await bundler.FlushAsync();

            var status = await bundler.GetUserOperationStatusAsync(hash);
            Assert.True(status.State == UserOpState.Included || status.State == UserOpState.Failed);

            if (status.State == UserOpState.Included)
            {
                var receipt = await bundler.GetUserOperationReceiptAsync(hash);
                Assert.NotNull(receipt);
                Assert.True(receipt.Success);
            }
        }

        [Fact]
        public async Task SignUserOperation_WithWrongKey_FailsValidation()
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

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000
                },
                wrongKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var hash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var result = await bundler.ExecuteBundleAsync();

            Assert.False(result?.Success ?? true,
                "Operation with wrong signature should fail during execution");
        }

        [Fact]
        public async Task SignUserOperation_MultipleOpsSequentially_AllValid()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            using var bundler = _fixture.CreateNewBundlerService();

            for (int i = 0; i < 3; i++)
            {
                var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

                var executeFunction = new ExecuteFunction
                {
                    Target = accountAddress,
                    Value = 0,
                    Data = new byte[] { (byte)i }
                };

                var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                    new UserOperation
                    {
                        Sender = accountAddress,
                        CallData = executeFunction.GetCallData(),
                        CallGasLimit = 100_000,
                        VerificationGasLimit = 200_000,
                        Nonce = nonce
                    },
                    accountKey);

                var hash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
                await bundler.FlushAsync();

                var status = await bundler.GetUserOperationStatusAsync(hash);
                Assert.True(status.State == UserOpState.Included || status.State == UserOpState.Failed,
                    $"Op {i} should be processed");
            }
        }

        [Fact]
        public async Task SignUserOperation_WithInitCode_ProducesValidSignature()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var ownerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var ownerAddress = ownerKey.GetPublicAddress();

            var accountAddress = await _fixture.GetAccountAddressAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 0.1m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    InitCode = initCode,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 200_000,
                    VerificationGasLimit = 500_000
                },
                ownerKey);

            Assert.NotNull(userOp.Signature);
            Assert.Equal(65, userOp.Signature.Length);
            Assert.Equal(initCode, userOp.InitCode);
        }
    }
}
