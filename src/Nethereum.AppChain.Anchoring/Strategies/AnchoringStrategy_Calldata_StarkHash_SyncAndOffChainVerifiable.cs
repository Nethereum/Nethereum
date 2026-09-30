using System;
using Nethereum.CoreChain.DataAvailability;

namespace Nethereum.AppChain.Anchoring.Strategies
{
    public class AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable : IAnchorSubmissionStrategy
    {
        public const int ProofHashSize = 32;
        private readonly CompressionAlgo _compression;

        public AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable(
            CompressionAlgo compression = CompressionAlgo.Brotli)
        {
            _compression = compression;
        }

        public string Name => "Calldata_StarkHash_SyncAndOffChainVerifiable";
        public AnchoringDataAvailability DataAvailability => AnchoringDataAvailability.Calldata;
        public AnchoringProofMode ProofMode => AnchoringProofMode.StarkHash;

        public AnchorSubmissionPayload BuildPayload(AnchorSubmissionContext context)
        {
            var hash = context.PipelineResult?.ProofPublication?.CommitmentHash ?? new byte[ProofHashSize];
            var blockData = context.BlockRlp;

            if (blockData != null && blockData.Length > 0)
            {
                var envelope = CompressedEnvelope.Wrap(blockData, _compression);
                var combined = new byte[ProofHashSize + envelope.Length];
                Array.Copy(hash, 0, combined, 0, ProofHashSize);
                Array.Copy(envelope, 0, combined, ProofHashSize, envelope.Length);

                return new AnchorSubmissionPayload
                {
                    ProofBytes = combined,
                    OnChainProofSystem = AnchoringOnChainProofSystem.StarkHashOffChain,
                    UncompressedSize = ProofHashSize + blockData.Length,
                    Description = $"STARK hash + {_compression}-compressed blocks ({combined.Length}b) — sync + off-chain verifiable"
                };
            }

            return new AnchorSubmissionPayload
            {
                ProofBytes = hash,
                OnChainProofSystem = AnchoringOnChainProofSystem.StarkHashOffChain,
                UncompressedSize = ProofHashSize,
                Description = $"STARK hash only ({ProofHashSize}b) — no block data for sync"
            };
        }
    }
}
