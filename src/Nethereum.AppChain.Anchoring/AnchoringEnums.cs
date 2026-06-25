namespace Nethereum.AppChain.Anchoring
{
    public enum AnchoringDataAvailability : byte
    {
        None = 0,
        Calldata = 1,
        BlobReference = 2
    }

    public enum AnchoringProofMode : byte
    {
        None = 0,
        StarkHash = 1,
        SnarkOnChain = 2
    }

    public enum AnchoringOnChainProofSystem : byte
    {
        NoProof = 0,
        StarkHashOffChain = 1,
        SnarkOnChain = 2
    }
}
