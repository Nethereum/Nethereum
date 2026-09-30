
namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_NoDA_NoProof_CommitmentOnly : IAnchorSubmissionStrategy
    {
        public string Name => "NoDA_NoProof_CommitmentOnly";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.None;
        public AnchoringProofMode ProofMode => AnchoringProofMode.None;

        public AnchorSubmissionPayload BuildPayload(AnchorSubmissionContext context)
        {
            return new AnchorSubmissionPayload
            {
                OnChainProofSystem = AnchoringOnChainProofSystem.NoProof,
                Description = "commitment only — no DA, no proof"
            };
        }
    }
}
