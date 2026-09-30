using System;

namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA : IAnchorSubmissionStrategy
    {
        public string Name => "BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.BlobReference;
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

            var blobHash = context.PipelineResult?.DaPublication?.Commitment?.CommitmentHash;
            if (blobHash != null && blobHash.Length > 0)
            {
                var combined = new byte[proofBytes.Length + blobHash.Length];
                Array.Copy(proofBytes, 0, combined, 0, proofBytes.Length);
                Array.Copy(blobHash, 0, combined, proofBytes.Length, blobHash.Length);

                return new AnchorSubmissionPayload
                {
                    ProofBytes = combined,
                    OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                    UncompressedSize = combined.Length,
                    Description = $"SNARK proof ({proofBytes.Length}b) + blob DA hash ({blobHash.Length}b) — trustless + blob DA"
                };
            }

            return new AnchorSubmissionPayload
            {
                ProofBytes = proofBytes,
                OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                UncompressedSize = proofBytes.Length,
                Description = $"SNARK proof ({proofBytes.Length}b) — trustless, no blob DA reference"
            };
        }
    }
}
