using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
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
                Assert.False(op.TryGetProperty("userOperation", out _), "Entries must not be wrapped in a userOperation envelope");

                var sender = op.GetProperty("sender").GetString();
                if (sender?.ToLower() == accountAddress.ToLower())
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, "Submitted operation should be in mempool");
        }

        [Fact]
        public async Task DumpMempool_ReturnsFlatUnpackedUserOperations()
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

            JsonElement? match = null;
            foreach (var op in mempool.EnumerateArray())
            {
                var sender = op.GetProperty("sender").GetString();
                if (sender?.ToLower() == accountAddress.ToLower())
                {
                    match = op;
                    break;
                }
            }

            Assert.NotNull(match);
            var userOperation = match!.Value;

            Assert.False(userOperation.TryGetProperty("userOperation", out _));
            Assert.False(userOperation.TryGetProperty("userOp", out _));

            Assert.True(userOperation.TryGetProperty("sender", out _));
            Assert.True(userOperation.TryGetProperty("nonce", out _));
            Assert.True(userOperation.TryGetProperty("callData", out _));
            Assert.True(userOperation.TryGetProperty("callGasLimit", out _));
            Assert.True(userOperation.TryGetProperty("verificationGasLimit", out _));
            Assert.True(userOperation.TryGetProperty("preVerificationGas", out _));
            Assert.True(userOperation.TryGetProperty("maxFeePerGas", out _));
            Assert.True(userOperation.TryGetProperty("maxPriorityFeePerGas", out _));
            Assert.True(userOperation.TryGetProperty("signature", out _));

            Assert.False(userOperation.TryGetProperty("accountGasLimits", out _));
            Assert.False(userOperation.TryGetProperty("gasFees", out _));
            Assert.False(userOperation.TryGetProperty("paymasterAndData", out _));
            Assert.False(userOperation.TryGetProperty("initCode", out _));

            Assert.False(userOperation.TryGetProperty("factory", out _));
            Assert.False(userOperation.TryGetProperty("factoryData", out _));
            Assert.False(userOperation.TryGetProperty("paymaster", out _));
            Assert.False(userOperation.TryGetProperty("paymasterVerificationGasLimit", out _));
            Assert.False(userOperation.TryGetProperty("paymasterPostOpGasLimit", out _));
            Assert.False(userOperation.TryGetProperty("paymasterData", out _));
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
                    Assert.False(rep.TryGetProperty("opsFailed", out _), "opsFailed must not leak into the reference wire shape");
                    Assert.Equal(1, rep.GetProperty("status").GetInt32());
                    break;
                }
            }

            Assert.True(found, "Set reputation should be retrievable");
        }

        [Fact]
        public async Task SetReputation_WithHexQuantityStrings_Succeeds()
        {
            var testAddress = "0x" + new string('f', 40);

            var setResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_setReputation",
                new[]
                {
                    new
                    {
                        address = testAddress,
                        opsIncluded = "0xa",
                        opsFailed = "0x2",
                        status = "ok"
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
                    Assert.False(rep.TryGetProperty("opsFailed", out _), "opsFailed must not leak into the reference wire shape");
                    break;
                }
            }

            Assert.True(found, "Reputation set via hex-quantity strings should be retrievable");
        }

        [Fact]
        public async Task SendBundleNow_EmptyMempool_ReturnsZeroLengthHexSentinel()
        {
            await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");
            await Task.Delay(100);

            var response = await _fixture.SendRpcRequestAsync("debug_bundler_sendBundleNow");

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);
            Assert.Equal(JsonValueKind.String, response.Result!.Value.ValueKind);
            Assert.Equal("0x", response.Result.Value.GetString());
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

        [Fact]
        public async Task ClearState_RemovesMempoolAndReputation()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                CreateUserOpObject(userOp),
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(sendResponse.Error);

            var setRepResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_setReputation",
                new[] { new { address = "0x" + new string('b', 40), opsIncluded = 5, opsFailed = 1, status = "ok" } },
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(setRepResponse.Error);

            var clearResponse = await _fixture.SendRpcRequestAsync("debug_bundler_clearState");
            Assert.Null(clearResponse.Error);
            Assert.Equal("ok", clearResponse.Result?.GetString());

            var dumpResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpMempool",
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(dumpResponse.Error);
            Assert.Equal(0, dumpResponse.Result!.Value.GetArrayLength());

            var repResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpReputation",
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(repResponse.Error);
            Assert.Equal(0, repResponse.Result!.Value.GetArrayLength());
        }

        [Fact]
        public async Task ClearMempool_RemovesPendingOpsOnly()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.CreateSignedUserOperationAsync(accountAddress, accountKey);
            var sendResponse = await _fixture.SendRpcRequestAsync(
                "eth_sendUserOperation",
                CreateUserOpObject(userOp),
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(sendResponse.Error);

            var clearResponse = await _fixture.SendRpcRequestAsync("debug_bundler_clearMempool");
            Assert.Null(clearResponse.Error);
            Assert.Equal("ok", clearResponse.Result?.GetString());

            var dumpResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpMempool",
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(dumpResponse.Error);
            Assert.Equal(0, dumpResponse.Result!.Value.GetArrayLength());
        }

        [Fact]
        public async Task ClearReputation_RemovesAllEntries()
        {
            var setResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_setReputation",
                new[] { new { address = "0x" + new string('c', 40), opsIncluded = 3, opsFailed = 0, status = "ok" } },
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(setResponse.Error);

            var clearResponse = await _fixture.SendRpcRequestAsync("debug_bundler_clearReputation");
            Assert.Null(clearResponse.Error);
            Assert.Equal("ok", clearResponse.Result?.GetString());

            var repResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpReputation",
                _fixture.EntryPointService.ContractAddress);
            Assert.Null(repResponse.Error);
            Assert.Equal(0, repResponse.Result!.Value.GetArrayLength());
        }

        [Fact]
        public async Task SetBundlingMode_ManualThenAuto_ReturnsOk()
        {
            var manualResponse = await _fixture.SendRpcRequestAsync("debug_bundler_setBundlingMode", "manual");
            Assert.Null(manualResponse.Error);
            Assert.Equal("ok", manualResponse.Result?.GetString());

            var autoResponse = await _fixture.SendRpcRequestAsync("debug_bundler_setBundlingMode", "auto");
            Assert.Null(autoResponse.Error);
            Assert.Equal("ok", autoResponse.Result?.GetString());
        }

        [Fact]
        public async Task SetBundlingMode_InvalidMode_ReturnsInvalidParams()
        {
            var response = await _fixture.SendRpcRequestAsync("debug_bundler_setBundlingMode", "sometimes");

            Assert.NotNull(response.Error);
            Assert.Equal(-32602, response.Error!.Code);
        }

        [Fact]
        public async Task DumpReputation_WithoutAddressParam_ReturnsFullDump()
        {
            var address1 = "0x" + new string('d', 40);
            var address2 = "0x" + new string('e', 40);

            await _fixture.SendRpcRequestAsync(
                "debug_bundler_setReputation",
                new[]
                {
                    new { address = address1, opsIncluded = 1, opsFailed = 0, status = "ok" },
                    new { address = address2, opsIncluded = 0, opsFailed = 7, status = "banned" }
                },
                _fixture.EntryPointService.ContractAddress);

            var repResponse = await _fixture.SendRpcRequestAsync(
                "debug_bundler_dumpReputation",
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(repResponse.Error);
            var addresses = repResponse.Result!.Value.EnumerateArray()
                .Select(rep => rep.GetProperty("address").GetString()!.ToLowerInvariant())
                .ToArray();

            Assert.Contains(address1, addresses);
            Assert.Contains(address2, addresses);
        }

        [Fact]
        public async Task GetStakeStatus_UnstakedAddress_ReturnsZeroStakeAndNotStaked()
        {
            var unstakedAddress = "0x" + new string('1', 40);

            var response = await _fixture.SendRpcRequestAsync(
                "debug_bundler_getStakeStatus",
                unstakedAddress,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result!.Value;
            var stakeInfo = result.GetProperty("stakeInfo");

            Assert.Equal(unstakedAddress.ToLower(), stakeInfo.GetProperty("addr").GetString()!.ToLower());
            Assert.Equal("0", stakeInfo.GetProperty("stake").GetString());
            Assert.Equal("0", stakeInfo.GetProperty("unstakeDelaySec").GetString());
            Assert.False(result.GetProperty("isStaked").GetBoolean());
        }

        [Fact]
        public async Task GetStakeStatus_StakedAddress_ReturnsStakeAndIsStaked()
        {
            var stakeAmount = Nethereum.Web3.Web3.Convert.ToWei(1m);
            const uint unstakeDelaySec = 86400;

            await _fixture.EntryPointService.AddStakeRequestAndWaitForReceiptAsync(
                new AddStakeFunction
                {
                    UnstakeDelaySec = unstakeDelaySec,
                    AmountToSend = stakeAmount
                });

            var stakerAddress = _fixture.BeneficiaryAddress;

            var response = await _fixture.SendRpcRequestAsync(
                "debug_bundler_getStakeStatus",
                stakerAddress,
                _fixture.EntryPointService.ContractAddress);

            Assert.Null(response.Error);
            Assert.NotNull(response.Result);

            var result = response.Result!.Value;
            var stakeInfo = result.GetProperty("stakeInfo");

            Assert.Equal(stakerAddress.ToLower(), stakeInfo.GetProperty("addr").GetString()!.ToLower());
            Assert.Equal(stakeAmount.ToString(), stakeInfo.GetProperty("stake").GetString());
            Assert.Equal(unstakeDelaySec.ToString(), stakeInfo.GetProperty("unstakeDelaySec").GetString());
            Assert.True(result.GetProperty("isStaked").GetBoolean());
        }

        private static object CreateUserOpObject(PackedUserOperation userOp) =>
            UserOperationTestHelper.CreateUserOpObject(userOp);
    }
}
