using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round5
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R5BundlerTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R5BundlerTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        private string EntryPoint => _fixture.EntryPointService.ContractAddress;

        [Fact]
        [Trait("Rule", "S7-R5-01")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task ExecuteBundle_WhenThePoolHoldsOpsOfTwoSupportedEntryPoints_StillIncludesTheOpOfTheFirstEntryPoint()
        {
            var secondEntryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new EntryPointDeployment());
            var config = _support.NewConfig();
            config.SupportedEntryPoints = new[] { EntryPoint, secondEntryPoint.ContractAddress };
            var mempool = new InMemoryUserOpMempool(5000);
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, mempool, null, null, null);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);
            var (_, foreignSender) = _fixture.GenerateNewAccount();
            await mempool.AddAsync(new MempoolEntry
            {
                UserOpHash = "0x" + new string('b', 64),
                EntryPoint = secondEntryPoint.ContractAddress,
                Priority = 1_000_000_000,
                UserOperation = new PackedUserOperation
                {
                    Sender = foreignSender,
                    Nonce = BigInteger.Zero,
                    InitCode = Array.Empty<byte>(),
                    CallData = Array.Empty<byte>(),
                    AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                    PreVerificationGas = 50_000,
                    GasFees = UserOperationBuilder.PackAccountGasLimits(1_000_000_000, 2_000_000_000),
                    PaymasterAndData = Array.Empty<byte>(),
                    Signature = new byte[65]
                }
            });

            var failure = await Record.ExceptionAsync(async () =>
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    await bundler.ExecuteBundleAsync();
                }
            });
            var status = await bundler.GetUserOperationStatusAsync(hash);

            Assert.Null(failure);
            Assert.Equal(UserOpState.Included, status.State);
        }

        [Fact]
        [Trait("Rule", "S7-R5-02")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task GetUserOperationReceipt_WhenAnotherBundlerIncludedAPooledOp_StillCreditsTheInclusionToTheSender()
        {
            var config = _support.NewConfig();
            var reputation = new InMemoryReputationService();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, null, null, reputation);
            using var other = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);
            await other.SendUserOperationAsync(op, EntryPoint);
            var execution = await other.ExecuteBundleAsync();

            var receipt = await bundler.GetUserOperationReceiptAsync(hash);
            var senderReputation = await bundler.GetReputationAsync(op.Sender);

            Assert.True(execution != null && execution.Success, execution?.Error);
            Assert.NotNull(receipt);
            Assert.True(senderReputation.OpsIncluded >= 1, "the op was included on chain and its inclusion was never credited");
        }

        [Fact]
        [Trait("Rule", "S7-R5-03")]
        [Trait("Erc", "ERC-7562 mempool")]
        public async Task SendUserOperation_WhenAnOpIsAdmitted_ReadsTheWholePendingPoolAtMostOnce()
        {
            var config = _support.NewConfig();
            var mempool = new CountingMempool(new InMemoryUserOpMempool(5000));
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, mempool, null, null, null);
            var (op, _) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);

            await bundler.SendUserOperationAsync(op, EntryPoint);

            Assert.True(mempool.PoolReads <= 1, $"one admission read the whole pending pool {mempool.PoolReads} times");
        }

        [Fact]
        [Trait("Rule", "S7-R5-04")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationReceipt")]
        public async Task GetUserOperationReceipt_WhenAPendingOpIsPolledRepeatedly_ScansTheLogsOnceNotOnEveryPoll()
        {
            var config = _support.NewConfig();
            config.ReceiptNegativeCacheSeconds = 3600;
            var counting = new CountingLogsClient(_fixture.Web3.Client);
            using var bundler = new BundlerService(new Nethereum.Web3.Web3(counting), config, null, null, null, null);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);

            for (var poll = 0; poll < 10; poll++)
            {
                Assert.Null(await bundler.GetUserOperationReceiptAsync(hash));
            }

            Assert.True(counting.LogQueries <= 1, $"10 polls of one pending op ran {counting.LogQueries} eth_getLogs scans");
        }

        [Fact]
        [Trait("Rule", "S7-R5-05")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task ExecuteBundle_WhenTheLogLookupOfAMinedOpFailsOnce_DoesNotLoseTheInclusionCreditOfTheSender()
        {
            var config = _support.NewConfig();
            var failing = new FailOnceLogsClient(_fixture.Web3.Client);
            var real = new BundleExecutor(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var reputation = new InMemoryReputationService();
            using var bundler = new BundlerService(new Nethereum.Web3.Web3(failing), config, null, null, new MinedThenLostExecutor(real), reputation);
            var (op, _) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);

            await Assert.ThrowsAsync<HttpRequestException>(() => bundler.ExecuteBundleAsync());
            await bundler.ExecuteBundleAsync();
            await bundler.ExecuteBundleAsync();
            var senderReputation = await bundler.GetReputationAsync(op.Sender);

            Assert.True(failing.Failures >= 1);
            Assert.True(senderReputation.OpsIncluded >= 1, "a transient node error while looking up the mined op dropped it without crediting the inclusion");
        }

        [Fact]
        [Trait("Rule", "S7-R5-06")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task ExecuteBundle_WhenALowerNonceOpOfTheSenderWasMinedElsewhere_NoLongerHoldsItPendingAfterTheLaterOpIsBundled()
        {
            var config = _support.NewConfig();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            using var other = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var (account, key) = await _support.AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var first = await _support.OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000);
            var second = await _support.OpAsync(account, key, nonce + 1, 1_000_000_000, 2_000_000_000);
            var firstHash = await bundler.SendUserOperationAsync(first, EntryPoint);
            var secondHash = await bundler.SendUserOperationAsync(second, EntryPoint);
            await other.SendUserOperationAsync(first, EntryPoint);
            var foreign = await other.ExecuteBundleAsync();

            var execution = await bundler.ExecuteBundleAsync();
            var secondStatus = await bundler.GetUserOperationStatusAsync(secondHash);
            var firstStatus = await bundler.GetUserOperationStatusAsync(firstHash);

            Assert.True(foreign != null && foreign.Success, foreign?.Error);
            Assert.True(execution != null && execution.Success, execution?.Error);
            Assert.Equal(UserOpState.Included, secondStatus.State);
            Assert.NotEqual(UserOpState.Pending, firstStatus.State);
        }

        private sealed class CountingMempool : IUserOpMempool, IMempoolEvictionLog, IMempoolInFlight, IMempoolPendingPool
        {
            private readonly InMemoryUserOpMempool _inner;
            private int _poolReads;

            public CountingMempool(InMemoryUserOpMempool inner)
            {
                _inner = inner;
            }

            public int PoolReads => Volatile.Read(ref _poolReads);

            public Task<MempoolAddOutcome> AddAsync(MempoolEntry entry) => _inner.AddAsync(entry);

            public Task<MempoolEntry?> GetAsync(string userOpHash) => _inner.GetAsync(userOpHash);

            public Task<MempoolEntry[]> GetPendingAsync(int maxCount, BigInteger? maxGas = null) => _inner.GetPendingAsync(maxCount, maxGas);

            public Task<MempoolEntry[]> GetAllPendingAsync()
            {
                Interlocked.Increment(ref _poolReads);
                return _inner.GetAllPendingAsync();
            }

            public Task<MempoolEntry[]> GetPendingPoolAsync()
            {
                Interlocked.Increment(ref _poolReads);
                return _inner.GetPendingPoolAsync();
            }

            public Task<MempoolEntry[]> GetBySenderAsync(string sender) => _inner.GetBySenderAsync(sender);

            public Task<bool> RemoveAsync(string userOpHash) => _inner.RemoveAsync(userOpHash);

            public Task MarkSubmittedAsync(string[] userOpHashes, string transactionHash) => _inner.MarkSubmittedAsync(userOpHashes, transactionHash);

            public Task MarkIncludedAsync(string[] userOpHashes, string transactionHash, BigInteger blockNumber, string? blockHash = null) =>
                _inner.MarkIncludedAsync(userOpHashes, transactionHash, blockNumber, blockHash);

            public Task MarkFailedAsync(string[] userOpHashes, string error) => _inner.MarkFailedAsync(userOpHashes, error);

            public Task RevertSubmittedAsync(string transactionHash) => _inner.RevertSubmittedAsync(transactionHash);

            public Task ClearAsync() => _inner.ClearAsync();

            public Task<int> CountAsync() => _inner.CountAsync();

            public Task<MempoolStats> GetStatsAsync() => _inner.GetStatsAsync();

            public Task<int> PruneAsync() => _inner.PruneAsync();

            public MempoolEntry[] DrainEvicted() => _inner.DrainEvicted();

            public Task<string[]> MarkInFlightAsync(string[] userOpHashes) => _inner.MarkInFlightAsync(userOpHashes);

            public Task ReleaseInFlightAsync(string[] userOpHashes) => _inner.ReleaseInFlightAsync(userOpHashes);
        }

        private sealed class MinedThenLostExecutor : IBundleExecutor
        {
            private readonly IBundleExecutor _inner;
            private bool _first = true;

            public MinedThenLostExecutor(IBundleExecutor inner)
            {
                _inner = inner;
            }

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) => _inner.BuildBundleAsync(entries);

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => _inner.ExecuteAsync(bundle);

            public Task<string> SubmitAsync(Bundle bundle) => _inner.SubmitAsync(bundle);

            public async Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash)
            {
                var result = await _inner.WaitForBundleReceiptAsync(bundle, transactionHash);
                if (_first)
                {
                    _first = false;
                    throw new HttpRequestException("node unreachable after the transaction was mined");
                }

                return result;
            }

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => _inner.EstimateBundleGasAsync(bundle);
        }

        private sealed class CountingLogsClient : ClientBase
        {
            private readonly IClient _inner;
            private int _logQueries;

            public CountingLogsClient(IClient inner)
            {
                _inner = inner;
            }

            public int LogQueries => Volatile.Read(ref _logQueries);

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null)
            {
                if (rpcRequestMessage.Method == "eth_getLogs") Interlocked.Increment(ref _logQueries);
                return _inner.SendAsync(rpcRequestMessage, route);
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new NotSupportedException();
        }

        private sealed class FailOnceLogsClient : ClientBase
        {
            private readonly IClient _inner;
            private int _armed = 1;
            private int _failures;

            public FailOnceLogsClient(IClient inner)
            {
                _inner = inner;
            }

            public int Failures => Volatile.Read(ref _failures);

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null)
            {
                if (rpcRequestMessage.Method == "eth_getLogs" && Interlocked.Exchange(ref _armed, 0) == 1)
                {
                    Interlocked.Increment(ref _failures);
                    throw new HttpRequestException("transient node failure");
                }

                return _inner.SendAsync(rpcRequestMessage, route);
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new NotSupportedException();
        }
    }
}
