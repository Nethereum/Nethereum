
namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_NoDA_SnarkOnChain_TrustlessVerification : IAnchorSubmissionStrategy
    {
        public string Name => "NoDA_SnarkOnChain_TrustlessVerification";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.None;
        public AnchoringProofMode ProofMode => AnchoringProofMode.SnarkOnChain;

        public AnchorSubmissionPayload BuildPayload(AnchorSubmissionContext context)
        {
            var proofBytes = context.PipelineResult?.ProofPublication?.SnarkProofBytes;
            if (proofBytes == null || proofBytes.Length == 0)
            {
                return new AnchorSubmissionPayload
                {
                    OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                    Description = "no SNARK proof available"
                };
            }

            return new AnchorSubmissionPayload
            {
                ProofBytes = proofBytes,
                OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                UncompressedSize = proofBytes.Length,
                Description = $"SNARK proof ({proofBytes.Length}b) — on-chain trustless verification"
            };
        }
    }
}
