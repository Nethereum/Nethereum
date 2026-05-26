using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Anchoring.Strategies;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.Strategies
{
    [Collection(DevChainAnchorFixture.COLLECTION_NAME)]
    public class AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable_Tests(
            DevChainAnchorFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task SubmitAndVerifyOnChain()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(
                    minimumProofSystem: (byte)AnchoringOnChainProofSystem.StarkHashOffChain);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                var b = await appchain.GetBlockByNumberAsync(5);
                var h = await appchain.GetBlockHashByNumberAsync(5);
                var fakeHash = new byte[32]; fakeHash[0] = 0xDE;

                var strategy = new AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable();
                var submission = strategy.BuildPayload(new AnchorSubmissionContext
                {
                    Scope = new AnchorScope { ChainId = (long)chainId, StartBlock = 1, EndBlock = 5,
                        StateRoot = b.StateRoot, BlockHash = h },
                    PipelineResult = new AnchorPublicationResult
                    {
                        ProofPublication = new ProofPublication { CommitmentHash = fakeHash }
                    }
                });

                Assert.Equal(32, submission.ProofBytes.Length);
                Assert.Equal(AnchoringOnChainProofSystem.StarkHashOffChain, submission.OnChainProofSystem);

                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash, b.ReceiptHash, h, submission);
                Assert.Equal(AnchorStatus.Confirmed, r.Status);
                Assert.Equal(5UL, (await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId)).EndBlock);
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task FullWorkerE2E()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                var strategy = new AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable();
                var worker = new AnchorWorker(new L1NodeChainAnchorable(appchain), svc,
                    new AnchorConfig { Enabled = true, ChainId = chainId, AnchorCadence = 5,
                        AnchorIntervalMs = 60000, AnchorContractAddress = Fixture.AnchorService.ContractAddress },
                    strategy: strategy);
                await worker.StartAsync(default);
                await worker.ForceAnchorAsync(5);

                Assert.Equal(5UL, (await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId)).EndBlock);
                await worker.StopAsync(default);
            }
            finally { appchain.Dispose(); }
        }
    }
}
