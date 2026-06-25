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
    public class AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable_Tests : StrategyContractTestBase
    {
        public AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable_Tests(
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
                var rlp = BlockHeaderEncoder.Current.Encode(b);
                var fakeHash = new byte[32]; fakeHash[0] = 0xAB;

                var strategy = new AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable();
                var submission = strategy.BuildPayload(new AnchorSubmissionContext
                {
                    Scope = new AnchorScope { ChainId = (long)chainId, StartBlock = 1, EndBlock = 5,
                        StateRoot = b.StateRoot, BlockHash = h },
                    BlockRlp = rlp,
                    PipelineResult = new AnchorPublicationResult
                    {
                        ProofPublication = new ProofPublication { CommitmentHash = fakeHash }
                    }
                });

                Assert.True(submission.ProofBytes.Length > 34);
                Assert.Equal(AnchoringOnChainProofSystem.StarkHashOffChain, submission.OnChainProofSystem);

                var extractedHash = new byte[32];
                System.Array.Copy(submission.ProofBytes, 0, extractedHash, 0, 32);
                Assert.Equal(fakeHash, extractedHash);

                var envelope = new byte[submission.ProofBytes.Length - 32];
                System.Array.Copy(submission.ProofBytes, 32, envelope, 0, envelope.Length);
                Assert.Equal(rlp, CompressedEnvelope.Unwrap(envelope));

                var r = await svc.AnchorBlockAsync(5, b.StateRoot, b.TransactionsHash, b.ReceiptHash, h, submission);
                Assert.Equal(AnchorStatus.Confirmed, r.Status);
            }
            finally { appchain.Dispose(); }
        }
    }
}
