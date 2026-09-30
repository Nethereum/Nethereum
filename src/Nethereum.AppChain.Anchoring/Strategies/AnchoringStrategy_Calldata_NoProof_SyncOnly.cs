using System;
using Nethereum.CoreChain.DataAvailability;

namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_Calldata_NoProof_SyncOnly : IAnchorSubmissionStrategy
    {
        public const int MaxProofSize = 4096;
        private readonly CompressionAlgo _compression;

        public AnchoringStrategy_Calldata_NoProof_SyncOnly(CompressionAlgo compression = CompressionAlgo.Brotli)
        {
            _compression = compression;
        }

        public string Name => "Calldata_NoProof_SyncOnly";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.Calldata;
        public AnchoringProofMode ProofMode => AnchoringProofMode.None;

        public AnchorSubmissionPayload BuildPayload(AnchorSubmissionContext context)
        {
            var data = context.BlockRlp;
            if (data == null || data.Length == 0)
            {
                return new AnchorSubmissionPayload
                {
                    OnChainProofSystem = AnchoringOnChainProofSystem.NoProof,
                    Description = "no block data available for calldata"
                };
            }

            var envelope = CompressedEnvelope.Wrap(data, _compression);
            if (envelope.Length > MaxProofSize)
            {
                return new AnchorSubmissionPayload
                {
                    OnChainProofSystem = AnchoringOnChainProofSystem.NoProof,
                    Description = $"compressed data exceeds {MaxProofSize}b contract limit ({envelope.Length}b)"
                };
            }

            return new AnchorSubmissionPayload
            {
                ProofBytes = envelope,
                OnChainProofSystem = AnchoringOnChainProofSystem.NoProof,
                UncompressedSize = data.Length,
                Description = $"{_compression}-compressed blocks for sync ({data.Length}b → {envelope.Length}b)"
            };
        }
    }
}
