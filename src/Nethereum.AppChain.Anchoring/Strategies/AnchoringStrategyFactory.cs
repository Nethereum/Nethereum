using System;
using Nethereum.CoreChain.DataAvailability;

namespace Nethereum.AppChain.Anchoring.Strategies
{
    public static class AnchoringStrategyFactory
    {
        public static IAnchorSubmissionStrategy Create(
            AnchoringDataAvailability da,
            AnchoringProofMode proof,
            CompressionAlgo compression = CompressionAlgo.Brotli)
        {
            return (da, proof) switch
            {
                (AnchoringDataAvailability.None, AnchoringProofMode.None)
                    => new AnchoringStrategy_NoDA_NoProof_CommitmentOnly(),

                (AnchoringDataAvailability.Calldata, AnchoringProofMode.None)
                    => new AnchoringStrategy_Calldata_NoProof_SyncOnly(compression),

                (AnchoringDataAvailability.None, AnchoringProofMode.StarkHash)
                    => new AnchoringStrategy_NoDA_StarkHash_OffChainVerifiable(),

                (AnchoringDataAvailability.Calldata, AnchoringProofMode.StarkHash)
                    => new AnchoringStrategy_Calldata_StarkHash_SyncAndOffChainVerifiable(compression),

                (AnchoringDataAvailability.None, AnchoringProofMode.SnarkOnChain)
                    => new AnchoringStrategy_NoDA_SnarkOnChain_TrustlessVerification(),

                (AnchoringDataAvailability.Calldata, AnchoringProofMode.SnarkOnChain)
                    => new AnchoringStrategy_Calldata_SnarkOnChain_SyncAndTrustlessVerification(compression),

                (AnchoringDataAvailability.BlobReference, AnchoringProofMode.SnarkOnChain)
                    => new AnchoringStrategy_BlobRef_SnarkOnChain_TrustlessVerificationWithBlobDA(),

                (AnchoringDataAvailability.BlobReference, AnchoringProofMode.None)
                    => throw new ArgumentException("BlobReference DA without a proof mode is not supported"),

                (AnchoringDataAvailability.BlobReference, AnchoringProofMode.StarkHash)
                    => throw new ArgumentException("BlobReference + StarkHash is redundant — STARK proof is already in blobs"),

                _ => throw new ArgumentException(
                    $"Unsupported strategy combination: DA={da}, Proof={proof}")
            };
        }
    }
}
