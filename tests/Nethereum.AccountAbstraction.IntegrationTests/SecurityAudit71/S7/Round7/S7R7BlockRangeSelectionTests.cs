using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round7
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R7BlockRangeSelectionTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R7BlockRangeSelectionTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        private async Task<(UserOpState State, string Hash)> RunAsync(ulong afterBlock, ulong untilBlock, ulong validUntil = 0)
        {
            var config = _support.NewConfig();
            var mempool = new InMemoryUserOpMempool(100);
            using var bundler = new BundlerService(
                _fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, mempool, new RangeValidator(afterBlock, untilBlock, validUntil), null, null);
            var (op, _) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            var hash = await bundler.SendUserOperationAsync(op, _fixture.EntryPointService.ContractAddress);

            await bundler.ExecuteBundleAsync();

            return ((await bundler.GetUserOperationStatusAsync(hash)).State, hash);
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData VALIDITY_BLOCK_RANGE_FLAG")]
        public async Task ExecuteBundle_WhenABlockRangeOpContainsTheNextBlock_BundlesIt()
        {
            var (state, _) = await RunAsync(0, 1_000_000_000UL);

            Assert.Equal(UserOpState.Included, state);
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._getValidationData VALIDITY_BLOCK_RANGE_FLAG")]
        public async Task ExecuteBundle_WhenABlockRangeOpHasAlreadyEnded_DropsItWithoutBundling()
        {
            var (state, _) = await RunAsync(0, 1UL);

            Assert.Equal(UserOpState.Dropped, state);
        }

        [Fact]
        [Trait("Rule", "S7-R7-01")]
        [Trait("Erc", "EntryPoint._validateAccountAndPaymasterValidationData")]
        public async Task ExecuteBundle_WhenTheAccountTimeRangeExpiredAndThePaymasterBlockRangeIsOpen_DoesNotBundleIt()
        {
            var (state, _) = await RunAsync(0, 1_000_000_000UL, validUntil: 1);

            Assert.NotEqual(UserOpState.Included, state);
        }

        private sealed class RangeValidator : IUserOpValidator
        {
            private readonly ulong _afterBlock;
            private readonly ulong _untilBlock;
            private readonly ulong _validUntil;

            public RangeValidator(ulong afterBlock, ulong untilBlock, ulong validUntil)
            {
                _afterBlock = afterBlock;
                _untilBlock = untilBlock;
                _validUntil = validUntil;
            }

            private UserOpValidationResult Result() => new()
            {
                IsValid = true,
                ValidUntil = _validUntil,
                ValidAfterBlock = _afterBlock,
                ValidUntilBlock = _untilBlock
            };

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
