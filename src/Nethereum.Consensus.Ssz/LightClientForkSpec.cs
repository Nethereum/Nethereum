namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// Per-fork constants for LightClient SSZ encoding, decoding, and merkle proof verification.
    /// Replaces the hardcoded Electra-shape constants previously on <see cref="SszBasicTypes"/>.
    /// Spec references:
    ///   Altair–Deneb: FINALIZED_ROOT_GINDEX = 105 (depth 6).
    ///   Electra+: FINALIZED_ROOT_GINDEX = 169 (depth 7) — EIP-7251 BeaconState shape change.
    /// </summary>
    public static class LightClientForkSpec
    {
        public const int FinalizedRootGIndexAltairToDeneb = 105;
        public const int FinalizedRootGIndexElectraPlus = 169;

        public const int CurrentSyncCommitteeBranchLength = 5;

        public const int ExecutionBranchDepth = 4;
        public const int ExecutionBranchIndex = 9;

        public static int FinalityBranchLength(ConsensusFork fork) =>
            fork >= ConsensusFork.Electra ? 7 : 6;

        public static int FinalityBranchDepth(ConsensusFork fork) =>
            FinalityBranchLength(fork);

        public static int FinalizedRootGIndex(ConsensusFork fork) =>
            fork >= ConsensusFork.Electra
                ? FinalizedRootGIndexElectraPlus
                : FinalizedRootGIndexAltairToDeneb;

        public static int FinalityBranchIndex(ConsensusFork fork) =>
            FinalizedRootGIndex(fork) - (1 << FinalityBranchDepth(fork));

        public static bool HasExecutionPayloadHeader(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        public static bool HasWithdrawalsRoot(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        public static bool HasBlobGasFields(ConsensusFork fork) =>
            fork >= ConsensusFork.Deneb;
    }
}
