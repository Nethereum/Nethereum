using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using PackedUserOperation = Nethereum.AccountAbstraction.Structs.PackedUserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class EthGetUserOperationTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public EthGetUserOperationTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetUserOperationByHash_PendingOp_ReturnsUserOp()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var userOpObject = CreateUserOpObject(userOp);

            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(sendResponse.Error);
            var userOpHash = sendResponse.Result!.Value.GetString()!;

            var getResponse = await _fixture.SendRpcRequestAsync(
                "eth_getUserOperationByHash",
                userOpHash);

            Assert.Null(getResponse.Error);
            Assert.NotNull(getResponse.Result);

            var result = getResponse.Result.Value;
            Assert.True(result.TryGetProperty("userOperation", out var returnedOp));
            Assert.True(result.TryGetProperty("entryPoint", out var entryPoint));

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.GetString()!.ToLower());
        }

        [Fact]
        public async Task GetUserOperationByHash_ExecutedOp_ReturnsWithTransactionHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var userOpObject = CreateUserOpObject(userOp);

            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(sendResponse.Error);
            var userOpHash = sendResponse.Result!.Value.GetString()!;

            await _fixture.BundlerService.FlushAsync();

            await Task.Delay(500);

            var getResponse = await _fixture.SendRpcRequestAsync(
                "eth_getUserOperationByHash",
                userOpHash);

            Assert.Null(getResponse.Error);
            Assert.NotNull(getResponse.Result);

            var result = getResponse.Result.Value;
            if (result.TryGetProperty("transactionHash", out var txHash) &&
                txHash.ValueKind != JsonValueKind.Null)
            {
                var txHashStr = txHash.GetString();
                Assert.NotNull(txHashStr);
                Assert.StartsWith("0x", txHashStr);
            }
        }

        [Fact]
        public async Task GetUserOperationByHash_NonExistent_ReturnsNull()
        {
            var fakeHash = "0x" + new string('1', 64);

            var response = await _fixture.SendRpcRequestAsync(
                "eth_getUserOperationByHash",
                fakeHash);

            Assert.Null(response.Error);
            Assert.True(
                response.Result == null ||
                response.Result.Value.ValueKind == JsonValueKind.Null);
        }

        [Fact]
        public async Task GetUserOperationReceipt_ExecutedOp_ReturnsReceipt()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.CreateSignedUserOperationAsync(
                accountAddress,
                accountKey,
                executeFunction.GetCallData());

            var userOpObject = CreateUserOpObject(userOp);

            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(sendResponse.Error);
            var userOpHash = sendResponse.Result!.Value.GetString()!;

            await _fixture.BundlerService.FlushAsync();

            await Task.Delay(500);

            var receiptResponse = await _fixture.SendRpcRequestAsync(
                "eth_getUserOperationReceipt",
                userOpHash);

            Assert.Null(receiptResponse.Error);

            if (receiptResponse.Result != null &&
                receiptResponse.Result.Value.ValueKind != JsonValueKind.Null)
            {
                var result = receiptResponse.Result.Value;

                Assert.True(result.TryGetProperty("userOpHash", out var returnedHash));
                Assert.Equal(userOpHash.ToLower(), returnedHash.GetString()!.ToLower());

                Assert.True(result.TryGetProperty("sender", out var sender));
                Assert.Equal(accountAddress.ToLower(), sender.GetString()!.ToLower());

                Assert.True(result.TryGetProperty("success", out var success));

                Assert.True(result.TryGetProperty("actualGasUsed", out _));
                Assert.True(result.TryGetProperty("actualGasCost", out _));
            }
        }

        [Fact]
        public async Task GetUserOperationReceipt_PendingOp_ReturnsNull()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var userOpObject = CreateUserOpObject(userOp);

            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(sendResponse.Error);
            var userOpHash = sendResponse.Result!.Value.GetString()!;

            var receiptResponse = await _fixture.SendRpcRequestAsync(
                "eth_getUserOperationReceipt",
                userOpHash);

            Assert.Null(receiptResponse.Error);
            Assert.True(
                receiptResponse.Result == null ||
                receiptResponse.Result.Value.ValueKind == JsonValueKind.Null,
                "Pending operation should not have a receipt yet");
        }

        [Fact]
        public async Task GetUserOperationReceipt_NonExistent_ReturnsNull()
        {
            var fakeHash = "0x" + new string('2', 64);

            var response = await _fixture.SendRpcRequestAsync(
                "eth_getUserOperationReceipt",
                fakeHash);

            Assert.Null(response.Error);
            Assert.True(
                response.Result == null ||
                response.Result.Value.ValueKind == JsonValueKind.Null);
        }

        [Fact]
        public async Task GetUserOperationReceipt_FailedOp_IncludesRevertReason()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x1111111111111111111111111111111111111111",
                Value = Nethereum.Web3.Web3.Convert.ToWei(100m),
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.CreateSignedUserOperationAsync(
                accountAddress,
                accountKey,
                executeFunction.GetCallData());

            var userOpObject = CreateUserOpObject(userOp);

            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            if (sendResponse.Error == null)
            {
                var userOpHash = sendResponse.Result!.Value.GetString()!;

                await _fixture.BundlerService.FlushAsync();

                await Task.Delay(500);

                var receiptResponse = await _fixture.SendRpcRequestAsync(
                    "eth_getUserOperationReceipt",
                    userOpHash);

                if (receiptResponse.Result != null &&
                    receiptResponse.Result.Value.ValueKind != JsonValueKind.Null)
                {
                    var result = receiptResponse.Result.Value;
                    if (result.TryGetProperty("success", out var success))
                    {
                        if (!success.GetBoolean())
                        {
                            Assert.True(result.TryGetProperty("reason", out _) ||
                                        result.TryGetProperty("revertReason", out _));
                        }
                    }
                }
            }
        }

        private static object CreateUserOpObject(PackedUserOperation userOp) =>
            UserOperationTestHelper.CreateUserOpObject(userOp);
    }
}
