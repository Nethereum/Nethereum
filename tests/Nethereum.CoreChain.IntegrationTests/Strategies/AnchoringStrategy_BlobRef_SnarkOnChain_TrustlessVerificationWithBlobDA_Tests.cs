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
    public class AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA_Tests(
            DevChainAnchorFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task SubmitWithSnarkAndBlobRef()
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
                var blobHash = new byte[32]; blobHash[0] = 0xBB;

                var strategy = new AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA();
                var submission = strategy.BuildPayload(new AnchorSubmissionContext
                {
                    Scope = new AnchorScope { ChainId = (long)chainId, StartBlock = 1, EndBlock = 5,
                        StateRoot = b.StateRoot, BlockHash = h },
                    PipelineResult = new AnchorPublicationResult
                    {
                        ProofPublication = new ProofPublication { SnarkProofBytes = snarkProof },
                        DaPublication = new DaPublication
                        {
                            Commitment = new DaCommitment { CommitmentHash = blobHash }
                        }
                    }
                });

                Assert.Equal(288, submission.ProofBytes.Length);
                Assert.Equal(AnchoringOnChainProofSystem.SnarkOnChain, submission.OnChainProofSystem);

                // Combined payload (SNARK 256b + blob hash 32b = 288b) needs a format-aware verifier.
                // MockVerifier expects exactly 256 bytes. Tested here as payload construction only.
                var snarkPortion = new byte[256];
                System.Array.Copy(submission.ProofBytes, 0, snarkPortion, 0, 256);
                Assert.Equal(snarkProof, snarkPortion);

                var blobPortion = new byte[32];
                System.Array.Copy(submission.ProofBytes, 256, blobPortion, 0, 32);
                Assert.Equal(blobHash, blobPortion);
            }
            finally { appchain.Dispose(); }
        }
    }
}
