using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Anchoring.Strategies;
using Nethereum.CoreChain.DataAvailability;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.Strategies
{
    [Collection(DevChainAnchorFixture.COLLECTION_NAME)]
    public class AnchoringStrategy_Calldata_NoProof_SyncOnly_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_Calldata_NoProof_SyncOnly_Tests(
            DevChainAnchorFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task SubmitAndVerifyOnChain()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                var b = await appchain.GetBlockByNumberAsync(5);
                var h = await appchain.GetBlockHashByNumberAsync(5);
                var rlp = BlockHeaderEncoder.Current.Encode(b);
                var envelope = CompressedEnvelope.Wrap(rlp, CompressionAlgo.Brotli);

                var submission = new AnchorSubmissionPayload
                {
                    ProofBytes = envelope,
                    OnChainProofSystem = AnchoringOnChainProofSystem.NoProof,
                    UncompressedSize = rlp.Length
                };

                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash, b.ReceiptHash, h, submission);
                Assert.Equal(AnchorStatus.Confirmed, r.Status);

                var onChain = await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId);
                Assert.Equal(5UL, onChain.EndBlock);

                Assert.Equal(rlp, CompressedEnvelope.Unwrap(submission.ProofBytes));
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task ChainContinuity()
        {
            var appchain = await ProduceAppChain(10);
            try
            {
                var (chainId, genesis) = await RegisterChain(minimumProofSystem: 0);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                for (int end = 5; end <= 10; end += 5)
                {
                    var b = await appchain.GetBlockByNumberAsync(end);
                    var h = await appchain.GetBlockHashByNumberAsync(end);
                    var rlp = BlockHeaderEncoder.Current.Encode(b);
                    var submission = new AnchorSubmissionPayload
                    {
                        ProofBytes = CompressedEnvelope.Wrap(rlp, CompressionAlgo.Brotli),
                        OnChainProofSystem = AnchoringOnChainProofSystem.NoProof
                    };
                    var r = await svc.AnchorBlockAsync(end, b.StateRoot, b.TransactionsHash,
                        b.ReceiptHash, h, submission);
                    Assert.Equal(AnchorStatus.Confirmed, r.Status);
                }
                Assert.Equal(10UL, (await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId)).EndBlock);
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
                var strategy = new AnchoringStrategy_Calldata_NoProof_SyncOnly();
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
