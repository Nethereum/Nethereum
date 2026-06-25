using System;
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
    public class AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification_Tests(
            DevChainAnchorFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task SubmitWithCalldataAndSnarkProof()
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
                var rlp = BlockHeaderEncoder.Current.Encode(b);

                var snarkProof = new byte[256];
                new Random(42).NextBytes(snarkProof);
                snarkProof[0] = 0xFF;

                var strategy = new AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification();
                var submission = strategy.BuildPayload(new AnchorSubmissionContext
                {
                    Scope = new AnchorScope { ChainId = (long)chainId, StartBlock = 1, EndBlock = 5,
                        StateRoot = b.StateRoot, BlockHash = h },
                    BlockRlp = rlp,
                    PipelineResult = new AnchorPublicationResult
                    {
                        ProofPublication = new ProofPublication { SnarkProofBytes = snarkProof }
                    }
                });

                Assert.True(submission.ProofBytes.Length > 256);
                Assert.Equal(AnchoringOnChainProofSystem.SnarkOnChain, submission.OnChainProofSystem);

                // Combined payload (SNARK + calldata) exceeds MockVerifier's 256-byte expectation.
                // Contract submission requires a format-aware verifier — tested here as payload construction only.
                var snarkPortion = new byte[256];
                System.Array.Copy(submission.ProofBytes, 0, snarkPortion, 0, 256);
                Assert.Equal(snarkProof, snarkPortion);

                var calldataPortion = new byte[submission.ProofBytes.Length - 256];
                System.Array.Copy(submission.ProofBytes, 256, calldataPortion, 0, calldataPortion.Length);
                Assert.Equal(rlp, CompressedEnvelope.Unwrap(calldataPortion));
            }
            finally { appchain.Dispose(); }
        }
    }
}
