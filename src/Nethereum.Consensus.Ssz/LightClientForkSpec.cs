using System;

namespace Nethereum.Consensus.Ssz
{
    public static class LightClientForkSpec
    {
        public const int MinSyncCommitteeParticipants = 1;

        public const int FinalizedRootGIndexAltairToDeneb = 105;

        /// <summary>
        /// <c>FINALIZED_ROOT_GINDEX_ELECTRA = 169</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 35. <c>floor(log2(169)) = 7</c>;
        /// the depth increase reflects the EIP-7251 BeaconState shape change.
        /// </summary>
        public const int FinalizedRootGIndexElectraPlus = 169;

        public const int CurrentSyncCommitteeGIndexAltairToDeneb = 54;

        public const int CurrentSyncCommitteeGIndexElectraPlus = 86;

        public const int NextSyncCommitteeGIndexAltairToDeneb = 55;

        public const int NextSyncCommitteeGIndexElectraPlus = 87;

        public const int ExecutionPayloadGIndex = 25;

        public static int FinalityBranchLength(ConsensusFork fork)
        {
            ThrowIfPostElectraNotImplemented(fork);
            return fork >= ConsensusFork.Electra ? 7 : 6;
        }

        public static int FinalityBranchDepth(ConsensusFork fork) =>
            FinalityBranchLength(fork);

        public static int FinalizedRootGIndex(ConsensusFork fork)
        {
            ThrowIfPostElectraNotImplemented(fork);
            return fork >= ConsensusFork.Electra
                ? FinalizedRootGIndexElectraPlus
                : FinalizedRootGIndexAltairToDeneb;
        }

        public static int FinalityBranchIndex(ConsensusFork fork) =>
            FinalizedRootGIndex(fork) - (1 << FinalityBranchDepth(fork));

        public static int CurrentSyncCommitteeBranchLength(ConsensusFork fork)
        {
            ThrowIfPostElectraNotImplemented(fork);
            return fork >= ConsensusFork.Electra ? 6 : 5;
        }

        public static int CurrentSyncCommitteeBranchDepth(ConsensusFork fork) =>
            CurrentSyncCommitteeBranchLength(fork);

        public static int CurrentSyncCommitteeGIndex(ConsensusFork fork)
        {
            ThrowIfPostElectraNotImplemented(fork);
            return fork >= ConsensusFork.Electra
                ? CurrentSyncCommitteeGIndexElectraPlus
                : CurrentSyncCommitteeGIndexAltairToDeneb;
        }

        public static int CurrentSyncCommitteeBranchIndex(ConsensusFork fork) =>
            CurrentSyncCommitteeGIndex(fork) - (1 << CurrentSyncCommitteeBranchDepth(fork));

        public static int NextSyncCommitteeBranchLength(ConsensusFork fork)
        {
            ThrowIfPostElectraNotImplemented(fork);
            return fork >= ConsensusFork.Electra ? 6 : 5;
        }

        public static int NextSyncCommitteeBranchDepth(ConsensusFork fork) =>
            NextSyncCommitteeBranchLength(fork);

        public static int NextSyncCommitteeGIndex(ConsensusFork fork)
        {
            ThrowIfPostElectraNotImplemented(fork);
            return fork >= ConsensusFork.Electra
                ? NextSyncCommitteeGIndexElectraPlus
                : NextSyncCommitteeGIndexAltairToDeneb;
        }

        public static int NextSyncCommitteeBranchIndex(ConsensusFork fork) =>
            NextSyncCommitteeGIndex(fork) - (1 << NextSyncCommitteeBranchDepth(fork));

        /// <summary>
        /// <c>floor(log2(EXECUTION_PAYLOAD_GINDEX)) = 4</c> Capella through Electra per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/capella/light-client/sync-protocol.md">
        /// specs/capella/light-client/sync-protocol.md</see>. Throws for pre-Capella forks
        /// (no <c>LightClientHeader.execution</c> field) and for Fulu/Gloas (post-EIP-7732
        /// reshape of <c>BeaconBlockBody</c> not yet specified).
        /// </summary>
        public static int ExecutionBranchDepth(ConsensusFork fork)
        {
            if (!HasExecutionPayloadHeader(fork))
                throw new InvalidOperationException(
                    $"Execution branch is not part of fork {fork}; pre-Capella has no LightClientHeader.execution field.");
            if (fork >= ConsensusFork.Gloas)
                throw new NotSupportedException(
                    $"Gloas execution-branch shape not yet specified (EIP-7732 reshapes BeaconBlockBody).");
            return 4;
        }

        public static int ExecutionBranchIndex(ConsensusFork fork) =>
            ExecutionPayloadGIndex - (1 << ExecutionBranchDepth(fork));

        public static bool HasExecutionPayloadHeader(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        public static bool HasExecutionPayloadContainer(ConsensusFork fork) =>
            fork >= ConsensusFork.Bellatrix;

        public static bool HasWithdrawalsRoot(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        public static bool HasBlobGasFields(ConsensusFork fork) =>
            fork >= ConsensusFork.Deneb;

        /// <summary>
        /// Guards getters that depend on the Electra LightClient shape from being called for
        /// Gloas. Fulu inherits Electra's LightClient gindices and branch lengths verbatim
        /// (the Fulu spec defines no <c>light-client/</c> overrides), so Fulu is permitted
        /// here and flows through the <c>fork &gt;= ConsensusFork.Electra</c> branch. Gloas
        /// is post-EIP-7732 and reshapes <c>BeaconBlockBody</c>; its LightClient spec is not
        /// yet stabilised, so we throw rather than silently emit Electra values.
        /// </summary>
        private static void ThrowIfPostElectraNotImplemented(ConsensusFork fork)
        {
            if (fork >= ConsensusFork.Gloas)
                throw new NotSupportedException(
                    $"Fork {fork} light-client container shape is not yet implemented; spec follow-up tracks Gloas/EIP-7732.");
        }
    }
}
