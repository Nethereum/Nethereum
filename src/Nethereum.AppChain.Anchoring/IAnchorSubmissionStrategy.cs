using Nethereum.CoreChain.DataAvailability;

namespace Nethereum.AppChain.Anchoring
{
    public class AnchorSubmissionContext
    {
        public AnchorScope Scope { get; init; } = null!;
        public AnchorPublicationResult? PipelineResult { get; init; }
        public byte[]? BlockRlp { get; init; }
    }

    public class AnchorSubmissionPayload
    {
        public byte[] ProofBytes { get; init; } = System.Array.Empty<byte>();
        public AnchoringOnChainProofSystem OnChainProofSystem { get; init; }
        public int UncompressedSize { get; init; }
        public string Description { get; init; } = "";
    }

    public interface IAnchorSubmissionStrategy
    {
        string Name { get; }
        AnchoringDataAvailability DataAvailability { get; }
        AnchoringProofMode ProofMode { get; }
        AnchorSubmissionPayload BuildPayload(AnchorSubmissionContext context);
    }
}
