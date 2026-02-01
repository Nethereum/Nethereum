using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class BundlerServiceTests
    {
        private readonly BundlerTestFixture _fixture;

        public BundlerServiceTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SendUserOperation_WithValidOperation_ReturnsUserOpHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(userOpHash);
            Assert.StartsWith("0x", userOpHash);
            Assert.Equal(66, userOpHash.Length);
        }

        [Fact]
        public async Task SendUserOperation_WithBlacklistedSender_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                BlacklistedAddresses = new HashSet<string> { accountAddress.ToLowerInvariant() }
            };

            using var bundler = new BundlerService(_fixture.Web3, config);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Contains("blacklisted", ex.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task SendUserOperation_WithUnsupportedEntryPoint_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var fakeEntryPoint = "0x0000000000000000000000000000000000000001";

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _fixture.BundlerService.SendUserOperationAsync(userOp, fakeEntryPoint));

            Assert.Contains("Unsupported EntryPoint", ex.Message);
        }

        [Fact]
        public async Task SendUserOperation_Duplicate_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var hash1 = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(hash1);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Contains("duplicate", ex.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task GetUserOperationByHash_AfterSubmission_ReturnsOperation()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var retrievedOp = await bundler.GetUserOperationByHashAsync(userOpHash);

            Assert.NotNull(retrievedOp);
            Assert.Equal(userOpHash, retrievedOp.UserOpHash);
            Assert.Equal(accountAddress.ToLowerInvariant(), retrievedOp.UserOperation.Sender?.ToLowerInvariant());
        }

        [Fact]
        public async Task GetUserOperationByHash_WithUnknownHash_ReturnsNull()
        {
            var unknownHash = "0x" + new string('0', 64);

            var result = await _fixture.BundlerService.GetUserOperationByHashAsync(unknownHash);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserOperationStatus_AfterSubmission_ReturnsPendingState()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var status = await bundler.GetUserOperationStatusAsync(userOpHash);

            Assert.NotNull(status);
            Assert.Equal(userOpHash, status.UserOpHash);
            Assert.Equal(UserOpState.Pending, status.State);
        }

        [Fact]
        public async Task GetPendingUserOperations_AfterSubmissions_ReturnsAllPending()
        {
            using var bundler = _fixture.CreateNewBundlerService();

            var pendingBefore = await bundler.GetPendingUserOperationsAsync();
            var initialCount = pendingBefore.Length;

            var salt1 = (ulong)Random.Shared.NextInt64();
            var (account1, key1) = await _fixture.CreateFundedAccountAsync(salt1);

            var salt2 = (ulong)Random.Shared.NextInt64();
            var key2 = new EthECKey(TestAccounts.Account3PrivateKey);
            var owner2 = key2.GetPublicAddress();
            await _fixture.FundAccountAsync(owner2, 0.1m);
            var result2 = await _fixture.AccountFactoryService.CreateAndDeployAccountAsync(
                owner2, owner2, _fixture.EntryPointService.ContractAddress, key2, 0.01m, salt2);

            var execute1 = new ExecuteFunction { Target = account1, Value = 0, Data = Array.Empty<byte>() };
            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation { Sender = account1, CallData = execute1.GetCallData(), CallGasLimit = 100_000, VerificationGasLimit = 100_000 },
                key1);

            var execute2 = new ExecuteFunction { Target = result2.AccountAddress, Value = 0, Data = Array.Empty<byte>() };
            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation { Sender = result2.AccountAddress, CallData = execute2.GetCallData(), CallGasLimit = 100_000, VerificationGasLimit = 100_000 },
                key2);

            await bundler.SendUserOperationAsync(userOp1, _fixture.EntryPointService.ContractAddress);
            await bundler.SendUserOperationAsync(userOp2, _fixture.EntryPointService.ContractAddress);

            var pending = await bundler.GetPendingUserOperationsAsync();

            Assert.Equal(initialCount + 2, pending.Length);
        }

        [Fact]
        public async Task DropUserOperation_WithValidHash_RemovesFromMempool()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var dropped = await bundler.DropUserOperationAsync(userOpHash);
            Assert.True(dropped);

            var status = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Dropped, status.State);
        }

        [Fact]
        public async Task SupportedEntryPoints_ReturnsConfiguredEntryPoints()
        {
            var entryPoints = await _fixture.BundlerService.SupportedEntryPointsAsync();

            Assert.NotNull(entryPoints);
            Assert.Single(entryPoints);
            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLowerInvariant(),
                entryPoints[0].ToLowerInvariant());
        }

        [Fact]
        public async Task ChainId_ReturnsConfiguredChainId()
        {
            var chainId = await _fixture.BundlerService.ChainIdAsync();

            Assert.Equal(_fixture.ChainId, chainId);
        }

        [Fact]
        public async Task GetStats_ReturnsValidStatistics()
        {
            var stats = await _fixture.BundlerService.GetStatsAsync();

            Assert.NotNull(stats);
            Assert.True(stats.StartedAt <= DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task Flush_ExecutesPendingOperations()
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
                    CallGasLimit = 200_000,
                    VerificationGasLimit = 200_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var statusBefore = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Pending, statusBefore.State);

            var txHash = await bundler.FlushAsync();

            Assert.NotNull(txHash);
            Assert.StartsWith("0x", txHash);

            var statusAfter = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.True(statusAfter.State == UserOpState.Included || statusAfter.State == UserOpState.Failed);
        }
    }
}
