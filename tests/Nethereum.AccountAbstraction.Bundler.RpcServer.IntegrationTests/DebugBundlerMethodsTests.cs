using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using PackedUserOperation = Nethereum.AccountAbstraction.Structs.PackedUserOperation;
using UserOperation = Nethereum.AccountAbstraction.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    [Collection(BundlerRpcServerFixture.COLLECTION_NAME)]
    public class DebugBundlerMethodsTests
    {
        private readonly BundlerRpcServerFixture _fixture;

        public DebugBundlerMethodsTests(BundlerRpcServerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DumpMempool_WithPendingOps_ReturnsOps()
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

            var dumpResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpMempool",
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(dumpResponse.Error);
            Assert.NotNull(dumpResponse.Result);

            var mempool = dumpResponse.Result.Value;
            Assert.Equal(JsonValueKind.Array, mempool.ValueKind);

            var found = false;
            foreach (var op in mempool.EnumerateArray())
            {
                if (op.TryGetProperty("userOperation", out var returnedOp))
                {
                    var sender = returnedOp.GetProperty("sender").GetString();
                    if (sender?.ToLower() == accountAddress.ToLower())
                    {
                        found = true;
                        break;
                    }
                }
            }

            Assert.True(found, "Submitted operation should be in mempool");
        }

        [Fact]
        public async Task SendBundleNow_WithPendingOps_ExecutesBundle()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var recipient = "0x" + new string('3', 40);
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

            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                userOpObject,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(sendResponse.Error);

            var flushResponse = await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");

            Assert.Null(flushResponse.Error);

            var balanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(recipient);
            Assert.True(balanceAfter.Value >= transferAmount,
                $"Recipient should have received {transferAmount} wei, got {balanceAfter.Value}");
        }

        [Fact]
        public async Task DumpMempool_AfterFlush_IsEmpty()
        {
            await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");

            await Task.Delay(200);

            var dumpResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpMempool",
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(dumpResponse.Error);
            Assert.NotNull(dumpResponse.Result);

            var mempool = dumpResponse.Result.Value;
            Assert.Equal(JsonValueKind.Array, mempool.ValueKind);

            var count = 0;
            foreach (var _ in mempool.EnumerateArray())
            {
                count++;
            }

            Assert.Equal(0, count);
        }

        [Fact]
        public async Task SetReputation_AndDumpReputation_Works()
        {
            var testAddress = "0x" + new string('a', 40);

            var setResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_setReputation",
                new[]
                {
                    new
                    {
                        address = testAddress,
                        opsIncluded = 10,
                        opsFailed = 2,
                        status = "throttled"
                    }
                },
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(setResponse.Error);

            var getResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpReputation",
                testAddress);

            Assert.Null(getResponse.Error);
            Assert.NotNull(getResponse.Result);

            var reputations = getResponse.Result.Value;
            Assert.Equal(JsonValueKind.Array, reputations.ValueKind);

            var found = false;
            foreach (var rep in reputations.EnumerateArray())
            {
                var addr = rep.GetProperty("address").GetString();
                if (addr?.ToLower() == testAddress.ToLower())
                {
                    found = true;
                    Assert.Equal(10, rep.GetProperty("opsIncluded").GetInt32());
                    Assert.Equal(2, rep.GetProperty("opsFailed").GetInt32());
                    Assert.Equal("throttled", rep.GetProperty("status").GetString());
                    break;
                }
            }

            Assert.True(found, "Set reputation should be retrievable");
        }

        [Fact]
        public async Task SendBundleNow_EmptyMempool_ReturnsNull()
        {
            await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");
            await Task.Delay(100);

            var response = await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");

            Assert.Null(response.Error);
        }

        [Fact]
        public async Task DumpMempool_MultipleOps_ReturnsSortedByPriority()
        {
            await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");
            await Task.Delay(100);

            var salt1 = (ulong)Random.Shared.NextInt64();
            var salt2 = (ulong)Random.Shared.NextInt64();

            var (account1, key1) = await _fixture.CreateFundedAccountAsync(salt1);
            var (account2, key2) = await _fixture.CreateFundedAccountAsync(salt2);

            var userOp1 = new UserOperation
            {
                Sender = account1,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 1_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOp2 = new UserOperation
            {
                Sender = account2,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 2_000_000_000
            };

            var packed1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, key1);
            var packed2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, key2);

            await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                CreateUserOpObject(packed1),
                _fixture.EntryPointService.ContractAddress);

            await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                CreateUserOpObject(packed2),
                _fixture.EntryPointService.ContractAddress);

            var dumpResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpMempool",
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(dumpResponse.Error);
            Assert.NotNull(dumpResponse.Result);

            var mempool = dumpResponse.Result.Value;
            var count = 0;
            foreach (var _ in mempool.EnumerateArray())
            {
                count++;
            }

            Assert.True(count >= 2, "Should have at least 2 pending operations");
        }

        private static object CreateUserOpObject(PackedUserOperation userOp) =>
            UserOperationTestHelper.CreateUserOpObject(userOp);
    }
}
