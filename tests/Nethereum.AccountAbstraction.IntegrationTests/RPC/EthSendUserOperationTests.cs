using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.RPC
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class EthSendUserOperationTests
    {
        private readonly BundlerTestFixture _fixture;

        public EthSendUserOperationTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SendUserOperation_ValidOp_ReturnsValidHash()
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

            var hash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(hash);
            Assert.True(hash.Length == 66);
            Assert.StartsWith("0x", hash);
        }

        [Fact]
        public async Task SendUserOperation_TrackedInMempool()
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

            var hash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var pending = await bundler.GetPendingUserOperationsAsync();

            Assert.Contains(pending, p => p.UserOpHash == hash);
        }

        [Fact]
        public async Task SendUserOperation_CanRetrieveByHash()
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

            var hash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var info = await bundler.GetUserOperationByHashAsync(hash);

            Assert.NotNull(info);
            Assert.Equal(hash, info.UserOpHash);
            Assert.Equal(accountAddress.ToLowerInvariant(), info.UserOperation.Sender?.ToLowerInvariant());
        }

        [Fact]
        public async Task SendUserOperation_ExecutesViaFlush()
        {
            var counterService = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var countBefore = await counterService.CountersQueryAsync(accountAddress);

            var countFunction = new CountFunction();
            var executeFunction = new ExecuteFunction
            {
                Target = counterService.ContractAddress,
                Value = 0,
                Data = countFunction.GetCallData()
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

            var hash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var txHash = await bundler.FlushAsync();

            Assert.NotNull(txHash);

            var countAfter = await counterService.CountersQueryAsync(accountAddress);
            Assert.Equal(countBefore + 1, countAfter);
        }

        [Fact]
        public async Task SendUserOperation_StatusTransitions_PendingToIncluded()
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

            var hash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var statusBefore = await bundler.GetUserOperationStatusAsync(hash);
            Assert.Equal(UserOpState.Pending, statusBefore.State);

            await bundler.FlushAsync();

            var statusAfter = await bundler.GetUserOperationStatusAsync(hash);
            Assert.True(statusAfter.State == UserOpState.Included || statusAfter.State == UserOpState.Failed);
            Assert.NotNull(statusAfter.TransactionHash);
        }

        [Fact]
        public async Task SendUserOperation_ReceiptAvailableAfterInclusion()
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

            var hash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var receiptBefore = await bundler.GetUserOperationReceiptAsync(hash);
            Assert.Null(receiptBefore);

            await bundler.FlushAsync();

            var status = await bundler.GetUserOperationStatusAsync(hash);
            if (status.State == UserOpState.Included)
            {
                var receiptAfter = await bundler.GetUserOperationReceiptAsync(hash);
                Assert.NotNull(receiptAfter);
                Assert.Equal(hash, receiptAfter.UserOpHash);
            }
        }

        [Fact]
        public async Task SendUserOperation_NonExistentHash_ReturnsDroppedStatus()
        {
            var nonExistentHash = "0x" + new string('0', 64);

            var status = await _fixture.BundlerService.GetUserOperationStatusAsync(nonExistentHash);

            Assert.Equal(UserOpState.Dropped, status.State);
            Assert.Contains("Not found", status.Error);
        }
    }
}
