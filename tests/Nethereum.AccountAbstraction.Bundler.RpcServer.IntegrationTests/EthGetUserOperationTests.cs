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

            Assert.False(returnedOp.TryGetProperty("factory", out _), "factory must be omitted when absent, not null");
            Assert.False(returnedOp.TryGetProperty("factoryData", out _), "factoryData must be omitted when absent, not null");
            Assert.False(returnedOp.TryGetProperty("paymaster", out _), "paymaster must be omitted when absent, not null");
            Assert.False(returnedOp.TryGetProperty("paymasterVerificationGasLimit", out _), "paymasterVerificationGasLimit must be omitted when absent, not null");
            Assert.False(returnedOp.TryGetProperty("paymasterPostOpGasLimit", out _), "paymasterPostOpGasLimit must be omitted when absent, not null");
            Assert.False(returnedOp.TryGetProperty("paymasterData", out _), "paymasterData must be omitted when absent, not null");

            Assert.True(returnedOp.TryGetProperty("sender", out _));
            Assert.True(returnedOp.TryGetProperty("nonce", out _));
            Assert.True(returnedOp.TryGetProperty("callData", out _));
            Assert.True(returnedOp.TryGetProperty("callGasLimit", out _));
            Assert.True(returnedOp.TryGetProperty("verificationGasLimit", out _));
            Assert.True(returnedOp.TryGetProperty("preVerificationGas", out _));
            Assert.True(returnedOp.TryGetProperty("maxFeePerGas", out _));
            Assert.True(returnedOp.TryGetProperty("maxPriorityFeePerGas", out _));
            Assert.True(returnedOp.TryGetProperty("signature", out _));

            Assert.True(result.TryGetProperty("blockNumber", out var blockNumber));
            Assert.Equal(JsonValueKind.Null, blockNumber.ValueKind);
            Assert.True(result.TryGetProperty("blockHash", out var blockHash));
            Assert.Equal(JsonValueKind.Null, blockHash.ValueKind);
            Assert.True(result.TryGetProperty("transactionHash", out var transactionHash));
            Assert.Equal(JsonValueKind.Null, transactionHash.ValueKind);
        }

        [Fact]
        public async Task GetUserOperationByHash_MissingUserOpHash_ReturnsSpecErrorMessage()
        {
            var response = await _fixture.SendRpcRequestAsync("eth_getUserOperationByHash", "");

            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal(-32602, response.Error!.Code);
            Assert.Contains("Missing/invalid userOpHash", response.Error.Message, StringComparison.OrdinalIgnoreCase);
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
            Assert.NotNull(receiptResponse.Result);
            Assert.NotEqual(JsonValueKind.Null, receiptResponse.Result!.Value.ValueKind);

            var result = receiptResponse.Result.Value;

            Assert.True(result.TryGetProperty("userOpHash", out var returnedHash));
            Assert.Equal(userOpHash.ToLower(), returnedHash.GetString()!.ToLower());

            Assert.True(result.TryGetProperty("sender", out var sender));
            Assert.Equal(accountAddress.ToLower(), sender.GetString()!.ToLower());

            Assert.True(result.TryGetProperty("success", out var success));
            Assert.True(success.GetBoolean(), "a mined operation must report event-derived success");

            Assert.False(result.TryGetProperty("reason", out _), "reason must be omitted for a successful op, not emitted as null");

            Assert.True(result.TryGetProperty("actualGasUsed", out var actualGasUsed));
            Assert.NotEqual("0x0", actualGasUsed.GetString());
            Assert.True(result.TryGetProperty("actualGasCost", out var actualGasCost));
            Assert.NotEqual("0x0", actualGasCost.GetString());

            Assert.True(result.TryGetProperty("logs", out var logs));
            Assert.Equal(JsonValueKind.Array, logs.ValueKind);
            Assert.True(logs.GetArrayLength() > 0, "the receipt must carry the operation's log slice");

            Assert.True(result.TryGetProperty("receipt", out var innerReceipt));
            Assert.True(innerReceipt.TryGetProperty("blockHash", out var blockHash));
            Assert.False(string.IsNullOrEmpty(blockHash.GetString()));

            // ERC-4337: receipt.receipt is the FULL bundle transaction receipt (B2-T30) - it must
            // carry logs/logsBloom/transactionIndex like eth_getTransactionReceipt, not the trimmed
            // subset the handler used to emit.
            Assert.True(innerReceipt.TryGetProperty("logs", out var innerLogs));
            Assert.Equal(JsonValueKind.Array, innerLogs.ValueKind);
            Assert.True(innerLogs.GetArrayLength() > 0, "the full tx receipt must carry all bundle logs");

            Assert.True(innerReceipt.TryGetProperty("logsBloom", out var logsBloom));
            Assert.False(string.IsNullOrEmpty(logsBloom.GetString()));
            Assert.StartsWith("0x", logsBloom.GetString());

            Assert.True(innerReceipt.TryGetProperty("transactionIndex", out var transactionIndex));
            Assert.False(string.IsNullOrEmpty(transactionIndex.GetString()));
            Assert.StartsWith("0x", transactionIndex.GetString());
        }

        [Fact]
        public async Task GetUserOperationReceipt_MissingUserOpHash_ReturnsSpecErrorMessage()
        {
            var response = await _fixture.SendRpcRequestAsync("eth_getUserOperationReceipt", "");

            Assert.Null(response.Result);
            Assert.NotNull(response.Error);
            Assert.Equal(-32602, response.Error!.Code);
            Assert.Contains("Missing/invalid userOpHash", response.Error.Message, StringComparison.OrdinalIgnoreCase);
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
                        if (!success.GetBoolean() && result.TryGetProperty("reason", out var reason))
                        {
                            Assert.Equal(JsonValueKind.String, reason.ValueKind);
                            Assert.False(string.IsNullOrEmpty(reason.GetString()));
                        }
                    }
                }
            }
        }

        private static object CreateUserOpObject(PackedUserOperation userOp) =>
            UserOperationTestHelper.CreateUserOpObject(userOp);
    }
}
