using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using Xunit;
using PackedUserOperation = Nethereum.AccountAbstraction.Structs.PackedUserOperation;
using UserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class EthSendUserOperationTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public EthSendUserOperationTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SendUserOperation_WithValidOp_ReturnsUserOpHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var userOpObject = CreateUserOpObject(userOp);

            var response = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var userOpHash = response.Result.Value.GetString();
            Assert.NotNull(userOpHash);
            Assert.StartsWith("0x", userOpHash);
            Assert.Equal(66, userOpHash.Length);
        }

        [Fact]
        public async Task SendUserOperation_WithExecuteCall_ExecutesOnChain()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var recipient = "0x1111111111111111111111111111111111111111";
            var transferAmount = Nethereum.Web3.Web3.Convert.ToWei(0.001m);

            var executeFunction = new ExecuteFunction
            {
                Target = recipient,
                Value = transferAmount,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.CreateSignedUserOperationAsync(
                accountAddress,
                accountKey,
                executeFunction.GetCallData());

            var userOpObject = CreateUserOpObject(userOp);

            var response = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response.Error);
            var userOpHash = response.Result!.Value.GetString()!;

            await _fixture.BundlerService.FlushAsync();

            var balanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(recipient);
            Assert.True(balanceAfter.Value >= transferAmount,
                $"Recipient balance {balanceAfter.Value} should be >= {transferAmount}");
        }

        [Fact]
        public async Task SendUserOperation_DuplicateOp_ReturnsError()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var userOpObject = CreateUserOpObject(userOp);

            var response1 = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response1.Error);

            var response2 = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(response2.Error);
        }

        [Fact]
        public async Task SendUserOperation_InvalidEntryPoint_ReturnsError()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var userOpObject = CreateUserOpObject(userOp);

            var invalidEntryPoint = "0x0000000000000000000000000000000000000000";

            var response = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                invalidEntryPoint);

            Assert.NotNull(response.Error);
        }

        [Fact]
        public async Task SendUserOperation_MultipleOpsFromSameAccount_QueuesCorrectly()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 2m);

            var nonce0 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                Nonce = nonce0,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
            var userOpObject1 = CreateUserOpObject(packedOp1);

            var response1 = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject1,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response1.Error);
            var hash1 = response1.Result!.Value.GetString()!;

            await _fixture.BundlerService.FlushAsync();

            var nonce1 = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, 0);
            Assert.Equal(nonce0 + 1, nonce1);

            var userOp2 = new UserOperation
            {
                Sender = accountAddress,
                Nonce = nonce1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, accountKey);
            var userOpObject2 = CreateUserOpObject(packedOp2);

            var response2 = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject2,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response2.Error);
            var hash2 = response2.Result!.Value.GetString()!;

            Assert.NotEqual(hash1, hash2);
        }

        private static object CreateUserOpObject(PackedUserOperation userOp) =>
            UserOperationTestHelper.CreateUserOpObject(userOp);
    }
}
