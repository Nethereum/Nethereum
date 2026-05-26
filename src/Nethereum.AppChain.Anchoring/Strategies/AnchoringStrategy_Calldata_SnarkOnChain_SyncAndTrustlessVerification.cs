using System;
using Nethereum.CoreChain.DataAvailability;

namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification : IAnchorSubmissionStrategy
    {
        private readonly CompressionAlgo _compression;

        public AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification(
            CompressionAlgo compression = CompressionAlgo.Brotli)
        {
            _compression = compression;
        }

        public string Name => "Calldata_SnarkOnChain_SyncAndTrustlessVerification";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.Calldata;
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

            var blockData = context.BlockRlp;
            if (blockData != null && blockData.Length > 0)
            {
                var envelope = CompressedEnvelope.Wrap(blockData, _compression);
                var combined = new byte[proofBytes.Length + envelope.Length];
                Array.Copy(proofBytes, 0, combined, 0, proofBytes.Length);
                Array.Copy(envelope, 0, combined, proofBytes.Length, envelope.Length);

                return new AnchorSubmissionPayload
                {
                    ProofBytes = combined,
                    OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                    UncompressedSize = proofBytes.Length + blockData.Length,
                    Description = $"SNARK proof ({proofBytes.Length}b) + {_compression}-compressed blocks — sync + trustless"
                };
            }

            return new AnchorSubmissionPayload
            {
                ProofBytes = proofBytes,
                OnChainProofSystem = AnchoringOnChainProofSystem.SnarkOnChain,
                UncompressedSize = proofBytes.Length,
                Description = $"SNARK proof ({proofBytes.Length}b) — trustless, no sync data"
            };
        }
    }
}
