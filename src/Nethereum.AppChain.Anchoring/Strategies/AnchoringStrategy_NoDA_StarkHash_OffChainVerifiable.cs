
namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable : IAnchorSubmissionStrategy
    {
        public string Name => "NoDA_StarkHash_OffChainVerifiable";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.None;
        public AnchoringProofMode ProofMode => AnchoringProofMode.StarkHash;

        public AnchorSubmissionPayload BuildPayload(AnchorSubmissionContext context)
        {
            var hash = context.PipelineResult?.ProofPublication?.CommitmentHash;
            if (hash == null || hash.Length == 0)
            {
                return new AnchorSubmissionPayload
                {
                    OnChainProofSystem = AnchoringOnChainProofSystem.StarkHashOffChain,
                    Description = "no STARK proof hash available"
                };
            }

            return new AnchorSubmissionPayload
            {
                ProofBytes = hash,
                OnChainProofSystem = AnchoringOnChainProofSystem.StarkHashOffChain,
                UncompressedSize = hash.Length,
                Description = $"STARK proof hash ({hash.Length}b) — proof in blobs, off-chain verifiable"
            };
        }
    }
}
