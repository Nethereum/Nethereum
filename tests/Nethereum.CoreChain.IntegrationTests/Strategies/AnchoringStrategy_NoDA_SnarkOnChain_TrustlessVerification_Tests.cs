using System;
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
    public class AnchoringStrategy_NoDA_SnarkOnChain_TrustlessVerification_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_NoDA_SnarkOnChain_TrustlessVerification_Tests(
            DevChainAnchorFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task SubmitWithMockVerifier()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(
                    minimumProofSystem: (byte)AnchoringOnChainProofSystem.SnarkOnChain);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                var b = await appchain.GetBlockByNumberAsync(5);
                var h = await appchain.GetBlockHashByNumberAsync(5);

                var snarkProof = new byte[256];
                new Random(42).NextBytes(snarkProof);
                snarkProof[0] = 0xFF;

                var strategy = new AnchoringStrategy_NoDA_SnarkOnChain_TrustlessVerification();
                var submission = strategy.BuildPayload(new AnchorSubmissionContext
                {
                    Scope = new AnchorScope { ChainId = (long)chainId, StartBlock = 1, EndBlock = 5,
                        StateRoot = b.StateRoot, BlockHash = h },
                    PipelineResult = new AnchorPublicationResult
                    {
                        ProofPublication = new ProofPublication { SnarkProofBytes = snarkProof }
                    }
                });

                Assert.Equal(256, submission.ProofBytes.Length);
                Assert.Equal(AnchoringOnChainProofSystem.SnarkOnChain, submission.OnChainProofSystem);

                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash, b.ReceiptHash, h, submission);
                Assert.Equal(AnchorStatus.Confirmed, r.Status);
                Assert.Equal(5UL, (await Fixture.AnchorService.GetLatestAnchorQueryAsync(chainId)).EndBlock);
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task RejectsInvalidProofSize()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(
                    minimumProofSystem: (byte)AnchoringOnChainProofSystem.SnarkOnChain);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                var b = await appchain.GetBlockByNumberAsync(5);
                var h = await appchain.GetBlockHashByNumberAsync(5);

                var wrong = new AnchorSubmissionPayload
                {
                    OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                    ProofBytes = new byte[] { 0xFF }
                };

                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash, b.ReceiptHash, h, wrong);
                Assert.Equal(AnchorStatus.Failed, r.Status);
            }
            finally { appchain.Dispose(); }
        }

        [Fact]
        public async Task RejectsEmptyProof()
        {
            var appchain = await ProduceAppChain(5);
            try
            {
                var (chainId, genesis) = await RegisterChain(
                    minimumProofSystem: (byte)AnchoringOnChainProofSystem.SnarkOnChain);
                var svc = CreateBatchService(chainId, genesis);
                await svc.InitializeAsync();

                var b = await appchain.GetBlockByNumberAsync(5);
                var h = await appchain.GetBlockHashByNumberAsync(5);

                var empty = new AnchorSubmissionPayload
                {
                    OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain
                };

                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash, b.ReceiptHash, h, empty);
                Assert.Equal(AnchorStatus.Failed, r.Status);
            }
            finally { appchain.Dispose(); }
        }
    }
}
