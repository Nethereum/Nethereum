using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter;
using Nethereum.AccountAbstraction.IntegrationTests.TestCounter.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;
using ExecuteFunction = Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition.ExecuteFunction;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class BundleExecutorTests
    {
        private readonly BundlerTestFixture _fixture;

        public BundleExecutorTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        private MempoolEntry CreateMempoolEntry(
            PackedUserOperation userOp,
            string userOpHash,
            string entryPoint,
            BigInteger? priority = null)
        {
            return new MempoolEntry
            {
                UserOpHash = userOpHash,
                UserOperation = userOp,
                EntryPoint = entryPoint,
                Priority = priority ?? 1_000_000_000,
                Prefund = 1_000_000_000_000_000,
                State = MempoolEntryState.Pending,
                SubmittedAt = DateTimeOffset.UtcNow
            };
        }

        [Fact]
        public async Task BuildBundleAsync_WithValidEntries_CreatesBundleWithCorrectMetadata()
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

            var hash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);
            var entry = CreateMempoolEntry(userOp, "0x" + BitConverter.ToString(hash).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);

            var bundle = await executor.BuildBundleAsync(new[] { entry });

            Assert.NotNull(bundle);
            Assert.Single(bundle.Entries);
            Assert.Equal(_fixture.EntryPointService.ContractAddress.ToLowerInvariant(), bundle.EntryPoint.ToLowerInvariant());
            Assert.Equal(_fixture.BeneficiaryAddress, bundle.Beneficiary);
            Assert.True(bundle.EstimatedGas > 0);
        }

        [Fact]
        public async Task BuildBundleAsync_WithMultipleEntries_CalculatesTotalGas()
        {
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

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp1);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp2);

            var entry1 = CreateMempoolEntry(userOp1, "0x" + BitConverter.ToString(hash1).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);
            var entry2 = CreateMempoolEntry(userOp2, "0x" + BitConverter.ToString(hash2).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);

            var bundle = await executor.BuildBundleAsync(new[] { entry1, entry2 });

            Assert.Equal(2, bundle.Entries.Length);
            Assert.True(bundle.EstimatedGas > 200_000);
        }

        [Fact]
        public async Task BuildBundleAsync_WithNoEntries_ThrowsException()
        {
            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                executor.BuildBundleAsync(Array.Empty<MempoolEntry>()));
        }

        [Fact]
        public async Task BuildBundleAsync_WithMixedEntryPoints_ThrowsException()
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

            var entry1 = CreateMempoolEntry(userOp, "0x" + new string('1', 64), _fixture.EntryPointService.ContractAddress);
            var entry2 = CreateMempoolEntry(userOp, "0x" + new string('2', 64), "0x0000000000000000000000000000000000000001");

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                executor.BuildBundleAsync(new[] { entry1, entry2 }));
        }

        [Fact]
        public async Task ExecuteAsync_WithValidBundle_ReturnsSuccessResult()
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

            var hash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);
            var entry = CreateMempoolEntry(userOp, "0x" + BitConverter.ToString(hash).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);
            var bundle = await executor.BuildBundleAsync(new[] { entry });

            var result = await executor.ExecuteAsync(bundle);

            Assert.True(result.Success);
            Assert.NotNull(result.TransactionHash);
            Assert.True(result.GasUsed > 0);
        }

        [Fact]
        public async Task ExecuteAsync_WithEmptyBundle_ReturnsFailureResult()
        {
            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);

            var bundle = new Bundle
            {
                Entries = Array.Empty<MempoolEntry>(),
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Beneficiary = _fixture.BeneficiaryAddress,
                EstimatedGas = 0,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await executor.ExecuteAsync(bundle);

            Assert.False(result.Success);
            Assert.Contains("Empty bundle", result.Error);
        }

        [Fact]
        public async Task ExecuteAsync_WithUnsupportedEntryPoint_ReturnsFailureResult()
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { "0x0000000000000000000000000000000000000001" },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                AutoBundleIntervalMs = 0
            };

            var executor = new BundleExecutor(_fixture.Web3, config);

            var bundle = new Bundle
            {
                Entries = new[] { new MempoolEntry { UserOpHash = "0x123", UserOperation = new PackedUserOperation() } },
                EntryPoint = "0x9999999999999999999999999999999999999999",
                Beneficiary = _fixture.BeneficiaryAddress,
                EstimatedGas = 100_000,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await executor.ExecuteAsync(bundle);

            Assert.False(result.Success);
            Assert.Contains("Unsupported EntryPoint", result.Error);
        }

        [Fact]
        public async Task ExecuteAsync_WithCounterContract_TracksStateChange()
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

            var hash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);
            var entry = CreateMempoolEntry(userOp, "0x" + BitConverter.ToString(hash).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);
            var bundle = await executor.BuildBundleAsync(new[] { entry });

            var result = await executor.ExecuteAsync(bundle);

            Assert.True(result.Success);

            var countAfter = await counterService.CountersQueryAsync(accountAddress);
            Assert.Equal(countBefore + 1, countAfter);
        }

        [Fact]
        public async Task ExecuteAsync_MultiplOperationsInBundle_ExecutesAll()
        {
            var counterService = await TestCounterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCounterDeployment());

            var salt1 = (ulong)Random.Shared.NextInt64();
            var (account1, key1) = await _fixture.CreateFundedAccountAsync(salt1, 0.1m);

            var salt2 = (ulong)Random.Shared.NextInt64();
            var key2 = new EthECKey(TestAccounts.Account3PrivateKey);
            var owner2 = key2.GetPublicAddress();
            await _fixture.FundAccountAsync(owner2, 0.1m);
            var result2 = await _fixture.AccountFactoryService.CreateAndDeployAccountAsync(
                owner2, owner2, _fixture.EntryPointService.ContractAddress, key2, 0.1m, salt2);

            var countFunction = new CountFunction();
            var execute1 = new ExecuteFunction
            {
                Target = counterService.ContractAddress,
                Value = 0,
                Data = countFunction.GetCallData()
            };
            var execute2 = new ExecuteFunction
            {
                Target = counterService.ContractAddress,
                Value = 0,
                Data = countFunction.GetCallData()
            };

            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = account1,
                    CallData = execute1.GetCallData(),
                    CallGasLimit = 200_000,
                    VerificationGasLimit = 200_000
                },
                key1);

            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = result2.AccountAddress,
                    CallData = execute2.GetCallData(),
                    CallGasLimit = 200_000,
                    VerificationGasLimit = 200_000
                },
                key2);

            var hash1 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp1);
            var hash2 = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp2);

            var entry1 = CreateMempoolEntry(userOp1, "0x" + BitConverter.ToString(hash1).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);
            var entry2 = CreateMempoolEntry(userOp2, "0x" + BitConverter.ToString(hash2).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);
            var bundle = await executor.BuildBundleAsync(new[] { entry1, entry2 });

            var bundleResult = await executor.ExecuteAsync(bundle);

            Assert.True(bundleResult.Success);

            var count1 = await counterService.CountersQueryAsync(account1);
            var count2 = await counterService.CountersQueryAsync(result2.AccountAddress);

            Assert.Equal(1, count1);
            Assert.Equal(1, count2);
        }

        [Fact]
        public async Task EstimateBundleGasAsync_ReturnsReasonableEstimate()
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
                    VerificationGasLimit = 100_000
                },
                accountKey);

            var hash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(userOp);
            var entry = CreateMempoolEntry(userOp, "0x" + BitConverter.ToString(hash).Replace("-", "").ToLower(), _fixture.EntryPointService.ContractAddress);

            var executor = new BundleExecutor(_fixture.Web3, _fixture.BundlerConfig);
            var bundle = await executor.BuildBundleAsync(new[] { entry });

            var estimatedGas = await executor.EstimateBundleGasAsync(bundle);

            Assert.True(estimatedGas > 21_000);
            Assert.True(estimatedGas < 30_000_000);
        }
    }
}
