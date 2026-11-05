using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round4
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R4BundlerTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R4BundlerTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        private string EntryPoint => _fixture.EntryPointService.ContractAddress;

        private static ulong FutureValidAfter() => (ulong)DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        private async Task<PackedUserOperation> RawOpAsync(byte[] initCode)
        {
            var (_, address) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(address, 1m);
            return new PackedUserOperation
            {
                Sender = address,
                Nonce = BigInteger.Zero,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
                PreVerificationGas = 50_000,
                GasFees = UserOperationBuilder.PackAccountGasLimits(1_000_000_000, 2_000_000_000),
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [Trait("Rule", "S7-R4-01")]
        [Trait("Erc", "ERC-7562 UREP-020")]
        public async Task SendUserOperation_WhenAnUnstakedFactoryAlreadyHasTheEntityLimitOfOps_RejectsTheNextOpWhateverTheValidityWindowOfTheOpsInThePool(ulong validAfterMode)
        {
            var validAfter = validAfterMode == 0 ? 0UL : FutureValidAfter();
            var config = _support.NewConfig();
            var validator = new FixedValidator(validAfter);
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, validator, null, null);
            var factory = "0x5555555555555555555555555555555555555555".HexToByteArray();
            var initCode = factory.Concat(new byte[] { 0x01 }).ToArray();
            var admitted = 0;

            for (var i = 0; i < config.MaxUnstakedEntityMempoolCount + 2; i++)
            {
                try
                {
                    await bundler.SendUserOperationAsync(await RawOpAsync(initCode), EntryPoint);
                    admitted++;
                }
                catch (BundlerRpcException)
                {
                    break;
                }
            }

            Assert.Equal(config.MaxUnstakedEntityMempoolCount, admitted);
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [Trait("Rule", "S7-R4-01")]
        [Trait("Erc", "ERC-7562 multiple roles")]
        public async Task SendUserOperation_WhenAnOpUsesAsItsFactoryTheSenderOfAnotherPooledOp_IsRejectedWhateverTheValidityWindowOfTheFirstOp(ulong validAfterMode)
        {
            var validAfter = validAfterMode == 0 ? 0UL : FutureValidAfter();
            var config = _support.NewConfig();
            var validator = new FixedValidator(validAfter);
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, validator, null, null);
            var first = await RawOpAsync(Array.Empty<byte>());
            await bundler.SendUserOperationAsync(first, EntryPoint);
            var initCode = first.Sender.HexToByteArray().Concat(new byte[] { 0x01 }).ToArray();
            var second = await RawOpAsync(initCode);

            await Assert.ThrowsAsync<BundlerRpcException>(() => bundler.SendUserOperationAsync(second, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R4-04")]
        [Trait("Erc", "ERC-4337 eth_getUserOperationReceipt")]
        public async Task GetUserOperationReceipt_WhenTheLookupOfAnUnknownHashIsStillScanningWhenTheOpIsAdmittedAndThenIncluded_ReturnsTheReceipt()
        {
            var config = _support.NewConfig();
            config.ReceiptNegativeCacheSeconds = 3600;
            var gated = new GatedLogsClient(_fixture.Web3.Client);
            var executor = new BundleExecutor(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            using var bundler = new BundlerService(new Nethereum.Web3.Web3(gated), config, null, null, executor, null);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);

            var lookup = Task.Run(() => bundler.GetUserOperationReceiptAsync(hash));
            await gated.Entered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await bundler.SendUserOperationAsync(op, EntryPoint);
            gated.Release.SetResult();
            Assert.Null(await lookup);
            var execution = await bundler.ExecuteBundleAsync();
            var receipt = await bundler.GetUserOperationReceiptAsync(hash);

            Assert.True(execution != null && execution.Success, execution?.Error);
            Assert.NotNull(receipt);
        }

        [Fact]
        [Trait("Rule", "S7-R4-03")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task ExecuteBundle_WhenTheBundleIsMinedButWaitingForItsReceiptFails_StillCreditsTheInclusionToTheSender()
        {
            var config = _support.NewConfig();
            var real = new BundleExecutor(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var executor = new MinedThenLostExecutor(real);
            var reputation = new InMemoryReputationService();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, null, executor, reputation);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);

            await Assert.ThrowsAsync<HttpRequestException>(() => bundler.ExecuteBundleAsync());
            await bundler.ExecuteBundleAsync();
            var onChain = await bundler.GetUserOperationReceiptAsync(hash);
            var senderReputation = await bundler.GetReputationAsync(op.Sender);

            Assert.NotNull(onChain);
            Assert.True(senderReputation.OpsIncluded >= 1, "the op was mined in the bundle transaction but its inclusion was never credited");
        }

        [Fact]
        [Trait("Rule", "S7-R4-03")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task ExecuteBundle_WhenTheBundleIsMinedAndTheReceiptWaitSucceeds_CreditsTheInclusionToTheSender()
        {
            var config = _support.NewConfig();
            var reputation = new InMemoryReputationService();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, null, null, reputation);
            var (op, _) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);

            var execution = await bundler.ExecuteBundleAsync();
            var senderReputation = await bundler.GetReputationAsync(op.Sender);

            Assert.True(execution != null && execution.Success, execution?.Error);
            Assert.True(senderReputation.OpsIncluded >= 1);
        }

        [Fact]
        [Trait("Rule", "S7-R4-02")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task ExecuteBundle_WhenAUserReplacesTheFirstOpOfAQueuedChainWhileTheBundleIsBuilt_DoesNotFailTheLaterOpOfTheSameChain()
        {
            var config = _support.NewConfig();
            var web3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var gated = new GatedBuildExecutor(new BundleExecutor(web3, config));
            using var bundler = new BundlerService(web3, config, null, null, gated, null);
            var (account, key) = await _support.AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            await bundler.SendUserOperationAsync(await _support.OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000), EntryPoint);
            var secondHash = await bundler.SendUserOperationAsync(
                await _support.OpAsync(account, key, nonce + 1, 1_000_000_000, 2_000_000_000), EntryPoint);

            var execution = Task.Run(() => bundler.ExecuteBundleAsync());
            await gated.BuildEntered.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await bundler.SendUserOperationAsync(await _support.OpAsync(account, key, nonce, 2_000_000_000, 3_000_000_000), EntryPoint);
            gated.Release.SetResult();
            await execution;
            var status = await bundler.GetUserOperationStatusAsync(secondHash);

            Assert.NotEqual(UserOpState.Failed, status.State);
        }

        [Fact]
        [Trait("Rule", "S7-R4-05")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task SendUserOperation_WhenTheSenderIsAddedToTheConfiguredBlacklistAfterTheBundlerWasCreated_IsRejectedAsBlacklisted()
        {
            var config = _support.NewConfig();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var (account, key) = await _support.AccountAsync();
            var nonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var op = await _support.OpAsync(account, key, nonce, 1_000_000_000, 2_000_000_000);
            config.BlacklistedAddresses.Add(account);

            var failure = await Assert.ThrowsAsync<BundlerRpcException>(() => bundler.SendUserOperationAsync(op, EntryPoint));

            Assert.Contains("blacklisted", failure.Message);
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

        private sealed class FixedValidator : IUserOpValidator
        {
            private readonly ulong _validAfter;

            public FixedValidator(ulong validAfter)
            {
                _validAfter = validAfter;
            }

            private UserOpValidationResult Result() => new() { IsValid = true, ValidAfter = _validAfter };

            public Task<UserOpValidationResult> ValidateAsync(PackedUserOperation userOp, string entryPoint) =>
                Task.FromResult(Result());

            public Task<UserOpValidationResult> ValidateAsync(
                PackedUserOperation userOp, string entryPoint, ISet<string> accessedStorageAddresses,
                Authorisation eip7702Auth = null) => Task.FromResult(Result());

            public Task<UserOpValidationResult> ValidateStructureAsync(
                PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null) =>
                Task.FromResult(Result());

            public Task<UserOpValidationResult> SimulateValidationAsync(
                PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null) =>
                Task.FromResult(Result());

            public Task<UserOpValidationResult> EstimateGasAsync(Nethereum.AccountAbstraction.UserOperation userOp, string entryPoint) =>
                Task.FromResult(Result());
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

        private sealed class GatedLogsClient : ClientBase
        {
            private readonly IClient _inner;
            private int _armed = 1;

            public GatedLogsClient(IClient inner)
            {
                _inner = inner;
            }

            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null)
            {
                if (rpcRequestMessage.Method == "eth_getLogs" && Interlocked.Exchange(ref _armed, 0) == 1)
                {
                    Entered.TrySetResult();
                    await Release.Task;
                }

                return await _inner.SendAsync(rpcRequestMessage, route);
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new NotSupportedException();
        }
    }
}
