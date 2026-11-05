using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round6
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R6BundlerTests
    {
        private const string Eip7702PrefixedFactory = "0x7702555555555555555555555555555555555555";

        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R6BundlerTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        private string EntryPoint => _fixture.EntryPointService.ContractAddress;

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

        private static byte[] FactoryInitCode(string factory) =>
            factory.HexToByteArray().Concat(new byte[] { 0x01 }).ToArray();

        [Fact]
        [Trait("Rule", "S7-R6-01")]
        [Trait("Erc", "ERC-4337 mempool")]
        public async Task ExecuteBundle_WhenTwoSupportedEntryPointsHoldPendingOps_BuildsABundleForEachOfThemInOneRound()
        {
            var secondEntryPoint = await EntryPointService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new EntryPointDeployment());
            var config = _support.NewConfig();
            config.SupportedEntryPoints = new[] { EntryPoint, secondEntryPoint.ContractAddress };
            var mempool = new InMemoryUserOpMempool(5000);
            var executor = new RecordingExecutor();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, mempool, null, executor, null);
            var (op, _) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
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

            await bundler.ExecuteBundleAsync();

            Assert.Contains(EntryPoint, executor.BuiltFor, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(secondEntryPoint.ContractAddress, executor.BuiltFor, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Rule", "S7-R6-02")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task ExecuteBundle_WhenAReceiptLookupRecordsTheInclusionBetweenSubmitAndMarkSubmitted_CreditsTheSenderOnce()
        {
            var config = _support.NewConfig();
            var real = new BundleExecutor(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var reputation = new InMemoryReputationService();
            var executor = new LookupAfterSubmitExecutor(real);
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, null, executor, reputation);
            var (op, hash) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            await bundler.SendUserOperationAsync(op, EntryPoint);
            executor.Lookup = () => bundler.GetUserOperationReceiptAsync(hash);

            var execution = await bundler.ExecuteBundleAsync();
            var senderReputation = await bundler.GetReputationAsync(op.Sender);

            Assert.True(execution != null && execution.Success, execution?.Error);
            Assert.NotNull(executor.LookupResult);
            Assert.Equal(1, senderReputation.OpsIncluded);
        }

        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-7562 UREP-020")]
        public async Task SendUserOperation_WhenAnUnstakedFactoryAddressStartsWith0x7702_StillAppliesTheEntityLimit()
        {
            var config = _support.NewConfig();
            var validator = new FixedValidator();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, validator, null, null);
            var initCode = FactoryInitCode(Eip7702PrefixedFactory);
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

        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-7562 reputation")]
        public async Task SendUserOperation_WhenAFactoryWhoseAddressStartsWith0x7702IsBanned_RejectsTheOp()
        {
            var config = _support.NewConfig();
            var reputation = new InMemoryReputationService();
            await reputation.SetBannedAsync(Eip7702PrefixedFactory, TimeSpan.FromHours(1));
            using var bundler = new BundlerService(
                _fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, null, new FixedValidator(), null, reputation);
            var op = await RawOpAsync(FactoryInitCode(Eip7702PrefixedFactory));

            await Assert.ThrowsAsync<BundlerRpcException>(() => bundler.SendUserOperationAsync(op, EntryPoint));
        }

        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-7562 entities")]
        public async Task GetFactoryInfo_WhenTheFactoryAddressStartsWith0x7702_ReturnsTheFactoryEntity()
        {
            var service = new StakingInfoService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), _support.NewConfig());

            var info = await service.GetFactoryInfoAsync(FactoryInitCode(Eip7702PrefixedFactory), EntryPoint);

            Assert.NotNull(info);
        }

        private sealed class RecordingExecutor : IBundleExecutor
        {
            private readonly List<string> _builtFor = new();

            public IReadOnlyList<string> BuiltFor => _builtFor;

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries)
            {
                _builtFor.Add(entries[0].EntryPoint);
                throw new InvalidOperationException("recording executor builds nothing");
            }

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => throw new NotSupportedException();

            public Task<string> SubmitAsync(Bundle bundle) => throw new NotSupportedException();

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                throw new NotSupportedException();

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => throw new NotSupportedException();
        }

        private sealed class LookupAfterSubmitExecutor : IBundleExecutor
        {
            private readonly IBundleExecutor _inner;

            public LookupAfterSubmitExecutor(IBundleExecutor inner)
            {
                _inner = inner;
            }

            public Func<Task<Nethereum.RPC.AccountAbstraction.DTOs.UserOperationReceipt?>>? Lookup { get; set; }

            public Nethereum.RPC.AccountAbstraction.DTOs.UserOperationReceipt? LookupResult { get; private set; }

            public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries) => _inner.BuildBundleAsync(entries);

            public Task<BundleExecutionResult> ExecuteAsync(Bundle bundle) => _inner.ExecuteAsync(bundle);

            public async Task<string> SubmitAsync(Bundle bundle)
            {
                var transactionHash = await _inner.SubmitAsync(bundle);
                if (Lookup != null)
                {
                    LookupResult = await Lookup();
                }

                return transactionHash;
            }

            public Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash) =>
                _inner.WaitForBundleReceiptAsync(bundle, transactionHash);

            public Task<BigInteger> EstimateBundleGasAsync(Bundle bundle) => _inner.EstimateBundleGasAsync(bundle);
        }

        private sealed class FixedValidator : IUserOpValidator
        {
            private static UserOpValidationResult Result() => new() { IsValid = true };

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
    }
}
