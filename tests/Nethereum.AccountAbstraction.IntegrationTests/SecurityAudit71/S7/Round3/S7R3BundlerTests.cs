using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R3BundlerTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R3BundlerTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        private string EntryPoint => _fixture.EntryPointService.ContractAddress;

        private BundlerService NewBundler(BundlerConfig config, IBundleExecutor? executor = null, IReputationService? reputation = null) =>
            new(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, null, executor, reputation);

        [Fact]
        [Trait("Rule", "S7-R3-02")]
        [Trait("Erc", "ERC-4337 bundle lifecycle")]
        public async Task ExecuteBundle_WhenWaitingForTheReceiptThrows_TheOpsAreNotLeftSubmittedForeverAndAreBundledAgain()
        {
            var config = _support.NewConfig();
            var web3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var executor = new LostTransactionExecutor(new BundleExecutor(web3, config));
            using var bundler = NewBundler(config, executor);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);

            await Assert.ThrowsAsync<HttpRequestException>(() => bundler.ExecuteBundleAsync());
            var status = await bundler.GetUserOperationStatusAsync(hash);
            var retry = await bundler.ExecuteBundleAsync();

            Assert.NotEqual(UserOpState.Submitted, status.State);
            Assert.True(retry != null && retry.Success, "the op stayed Submitted and was never bundled again: " + retry?.Error);
        }

        [Fact]
        [Trait("Rule", "S7-R3-03")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task ExecuteBundle_WhenAHigherPayingOpEvictsACandidateBeforeTheBundleIsMarkedInFlight_TheEvictedOpIsNotExecuted()
        {
            var config = _support.NewConfig(maxMempoolSize: 2);
            var web3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var gated = new GatedBuildExecutor(new BundleExecutor(web3, config));
            using var bundler = NewBundler(config, gated, new InMemoryReputationService());
            var candidates = new List<(string Account, string Hash, BigInteger StartNonce)>();
            foreach (var _ in new[] { 0, 1 })
            {
                var (account, key) = await _support.AccountAsync();
                var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
                var op = await _support.OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000);
                candidates.Add((account, await bundler.SendUserOperationAsync(op, EntryPoint), nonce));
            }

            var execution = Task.Run(() => bundler.ExecuteBundleAsync());
            await gated.BuildEntered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var (richAccount, richKey) = await _support.AccountAsync();
            var richNonce = await _fixture.EntryPointService.GetNonceQueryAsync(richAccount, BigInteger.Zero);
            await bundler.SendUserOperationAsync(
                await _support.OpAsync(richAccount, richKey, richNonce, 5_000_000_000, 6_000_000_000), EntryPoint);
            gated.Release.SetResult();
            await execution;

            var evicted = new List<(string Account, BigInteger StartNonce)>();
            foreach (var (account, hash, startNonce) in candidates)
            {
                if (await bundler.GetUserOperationByHashAsync(hash) == null) evicted.Add((account, startNonce));
            }
            Assert.Single(evicted);
            var onChainNonce = await _fixture.EntryPointService.GetNonceQueryAsync(evicted[0].Account, BigInteger.Zero);
            Assert.True(onChainNonce == evicted[0].StartNonce, "the op the mempool evicted was still bundled and executed on chain");
        }

        [Fact]
        [Trait("Rule", "S7-R3-04")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationReceipt")]
        public async Task GetUserOperationReceipt_WhenTheHashWasAskedBeforeTheOpWasSentAndTheOpIsThenIncluded_ReturnsTheReceipt()
        {
            var config = _support.NewConfig();
            config.ReceiptNegativeCacheSeconds = 3600;
            using var bundler = NewBundler(config);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);

            Assert.Null(await bundler.GetUserOperationReceiptAsync(hash));
            await bundler.SendUserOperationAsync(op, EntryPoint);
            var execution = await bundler.ExecuteBundleAsync();
            var receipt = await bundler.GetUserOperationReceiptAsync(hash);

            Assert.True(execution != null && execution.Success, execution?.Error);
            Assert.NotNull(receipt);
        }

        [Fact]
        [Trait("Rule", "S7-R3-05")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationReceipt")]
        public async Task GetUserOperationReceipt_WhenTheSameUnknownHashExpiresAndIsAskedAgainManyTimes_DoesNotRetainMemoryPerLookup()
        {
            var config = _support.NewConfig();
            config.ReceiptNegativeCacheSeconds = 0;
            using var bundler = new BundlerService(new Nethereum.Web3.Web3(new PassThroughRpcClient(_fixture.Web3.Client)), config);
            var hash = "0x" + new string('e', 64);
            for (var i = 0; i < 200; i++) await bundler.GetUserOperationReceiptAsync(hash);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(true);

            for (var i = 0; i < 40_000; i++) await bundler.GetUserOperationReceiptAsync(hash);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var retained = GC.GetTotalMemory(true) - before;

            Assert.True(retained < 1_500_000, $"{retained} bytes retained by 40000 lookups of one hash");
        }

        [Fact]
        [Trait("Rule", "S7-R3-09")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task SendUserOperation_WhenTheBlacklistHoldsTheSenderInAChecksumCase_IsRejectedAsBlacklisted()
        {
            var config = _support.NewConfig();
            var (account, key) = await _support.AccountAsync();
            var checksummed = new Nethereum.Util.AddressUtil().ConvertToChecksumAddress(account);
            Assert.NotEqual(checksummed, checksummed.ToLowerInvariant());
            config.BlacklistedAddresses = new HashSet<string> { checksummed };
            using var bundler = NewBundler(config);
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await _support.OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000);

            var failure = await Assert.ThrowsAsync<BundlerRpcException>(() => bundler.SendUserOperationAsync(op, EntryPoint));

            Assert.Contains("blacklisted", failure.Message);
        }

        private sealed class LostTransactionExecutor : IBundleExecutor
        {
            private const string LostHash = "0xdddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
            private readonly IBundleExecutor _inner;
            private bool _first = true;

            public LostTransactionExecutor(IBundleExecutor inner)
            {
                _inner = inner;
            }

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) => _inner.BuildBundleAsync(entries);

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => _inner.ExecuteAsync(bundle);

            public Task<string> SubmitAsync(Bundle bundle)
            {
                if (_first)
                {
                    _first = false;
                    return Task.FromResult(LostHash);
                }

                return _inner.SubmitAsync(bundle);
            }

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash)
            {
                if (transactionHash == LostHash) throw new HttpRequestException("node unreachable");
                return _inner.WaitForBundleReceiptAsync(bundle, transactionHash);
            }

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => _inner.EstimateBundleGasAsync(bundle);
        }

        private sealed class GatedBuildExecutor : IBundleExecutor
        {
            private readonly IBundleExecutor _inner;

            public GatedBuildExecutor(IBundleExecutor inner)
            {
                _inner = inner;
            }

            public TaskCompletionSource BuildEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task<Bundle> BuildBundleAsync(MempoolEntry[] entries)
            {
                BuildEntered.TrySetResult();
                await Release.Task;
                return await _inner.BuildBundleAsync(entries);
            }

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => _inner.ExecuteAsync(bundle);

            public Task<string> SubmitAsync(Bundle bundle) => _inner.SubmitAsync(bundle);

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                _inner.WaitForBundleReceiptAsync(bundle, transactionHash);

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => _inner.EstimateBundleGasAsync(bundle);
        }

        private sealed class PassThroughRpcClient : ClientBase
        {
            private readonly IClient _inner;

            public PassThroughRpcClient(IClient inner)
            {
                _inner = inner;
            }

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
                _inner.SendAsync(rpcRequestMessage, route);

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new NotSupportedException();
        }
    }
}
