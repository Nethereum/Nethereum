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

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round8
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R8BlockWindowTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R8BlockWindowTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        [Fact]
        [Trait("Rule", "S7-R8-03")]
        [Trait("Erc", "EntryPoint._getValidationData VALIDITY_BLOCK_RANGE_FLAG")]
        public async Task ExecuteBundle_WhenEndedBlockWindowOpsOutrankAnHonestOp_StillBundlesTheHonestOpInTheSameCycle()
        {
            var config = _support.NewConfig();
            config.MaxBundleSize = 2;
            var mempool = new InMemoryUserOpMempool(100);
            var ended = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var bundler = new BundlerService(
                _fixture.Node.CreateWeb3(_fixture.BundlerAccount), config, mempool, new EndedWindowValidator(ended), null, null);

            var (endedOne, _) = await _support.FreshOpAsync(5_000_000_000, 6_000_000_000);
            var (endedTwo, _) = await _support.FreshOpAsync(5_000_000_000, 6_000_000_000);
            var (honest, _) = await _support.FreshOpAsync(1_000_000_000, 2_000_000_000);
            ended.Add(endedOne.Sender);
            ended.Add(endedTwo.Sender);

            await bundler.SendUserOperationAsync(endedOne, _fixture.EntryPointService.ContractAddress);
            await bundler.SendUserOperationAsync(endedTwo, _fixture.EntryPointService.ContractAddress);
            var honestHash = await bundler.SendUserOperationAsync(honest, _fixture.EntryPointService.ContractAddress);

            await bundler.ExecuteBundleAsync();

            Assert.Equal(UserOpState.Included, (await bundler.GetUserOperationStatusAsync(honestHash)).State);
        }

        private sealed class EndedWindowValidator : IUserOpValidator
        {
            private readonly HashSet<string> _ended;

            public EndedWindowValidator(HashSet<string> ended)
            {
                _ended = ended;
            }

            private UserOpValidationResult Result(PackedUserOperation userOp) => _ended.Contains(userOp.Sender)
                ? new UserOpValidationResult { IsValid = true, ValidAfterBlock = 0, ValidUntilBlock = 1 }
                : new UserOpValidationResult { IsValid = true };

            public Task<UserOpValidationResult> ValidateAsync(PackedUserOperation userOp, string entryPoint) =>
                Task.FromResult(Result(userOp));

            public Task<UserOpValidationResult> ValidateAsync(
                PackedUserOperation userOp, string entryPoint, ISet<string> accessedStorageAddresses,
                Authorisation eip7702Auth = null) => Task.FromResult(Result(userOp));

            public Task<UserOpValidationResult> ValidateStructureAsync(
                PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null) =>
                Task.FromResult(Result(userOp));

            public Task<UserOpValidationResult> SimulateValidationAsync(
                PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null) =>
                Task.FromResult(Result(userOp));

            public Task<UserOpValidationResult> EstimateGasAsync(Nethereum.AccountAbstraction.UserOperation userOp, string entryPoint) =>
                Task.FromResult(new UserOpValidationResult { IsValid = true });
        }
    }
}
