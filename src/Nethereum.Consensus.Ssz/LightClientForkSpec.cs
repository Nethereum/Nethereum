using System;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// Per-fork constants for LightClient SSZ encoding, decoding, and merkle proof verification.
    /// All gindices and depths are taken from the canonical <c>specs/altair/light-client/sync-protocol.md</c>,
    /// <c>specs/capella/light-client/sync-protocol.md</c>, and <c>specs/electra/light-client/sync-protocol.md</c>
    /// documents in the consensus-specs repository. The pre-Capella subset
    /// (Phase0/Altair) intentionally throws for execution-branch queries because no
    /// <c>LightClientHeader.execution</c> field exists per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see>.
    /// </summary>
    public static class LightClientForkSpec
    {
        /// <summary>
        /// <c>FINALIZED_ROOT_GINDEX = 105</c> Altair–Deneb per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 71. <c>floor(log2(105)) = 6</c>.
        /// </summary>
        public const int FinalizedRootGIndexAltairToDeneb = 105;

        /// <summary>
        /// <c>FINALIZED_ROOT_GINDEX_ELECTRA = 169</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 35. <c>floor(log2(169)) = 7</c>;
        /// the depth increase reflects the EIP-7251 BeaconState shape change.
        /// </summary>
        public const int FinalizedRootGIndexElectraPlus = 169;

        /// <summary>
        /// <c>CURRENT_SYNC_COMMITTEE_GINDEX = 54</c> Altair–Deneb per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 70. <c>floor(log2(54)) = 5</c>.
        /// </summary>
        public const int CurrentSyncCommitteeGIndexAltairToDeneb = 54;

        /// <summary>
        /// <c>CURRENT_SYNC_COMMITTEE_GINDEX_ELECTRA = 86</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56. <c>floor(log2(86)) = 6</c>.
        /// </summary>
        public const int CurrentSyncCommitteeGIndexElectraPlus = 86;

        /// <summary>
        /// <c>NEXT_SYNC_COMMITTEE_GINDEX = 55</c> Altair–Deneb per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 70. <c>floor(log2(55)) = 5</c>.
        /// </summary>
        public const int NextSyncCommitteeGIndexAltairToDeneb = 55;

        /// <summary>
        /// <c>NEXT_SYNC_COMMITTEE_GINDEX_ELECTRA = 87</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56. <c>floor(log2(87)) = 6</c>.
        /// </summary>
        public const int NextSyncCommitteeGIndexElectraPlus = 87;

        /// <summary>
        /// <c>EXECUTION_PAYLOAD_GINDEX = 25</c> Capella+ per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/capella/light-client/sync-protocol.md">
        /// specs/capella/light-client/sync-protocol.md</see>. <c>floor(log2(25)) = 4</c>;
        /// subtree index = 25 - 16 = 9.
        /// </summary>
        public const int ExecutionPayloadGIndex = 25;

        /// <summary>
        /// <c>floor(log2(FINALIZED_ROOT_GINDEX))</c>: 6 Altair–Deneb, 7 Electra+. Used as both
        /// the merkle proof depth and the wire-format <c>Vector[Bytes32, N]</c> length per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 71 +
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 35.
        /// </summary>
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

        /// <summary>
        /// Wire-format vector length for the <c>current_sync_committee_branch</c> in
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 70 (5 Altair–Deneb) +
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56 (6 Electra+).
        /// </summary>
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

        /// <summary>
        /// <c>subtree_index = gindex - (1 &lt;&lt; depth)</c>. Resolves to 22 for both
        /// Altair–Deneb (54 - 32) and Electra+ (86 - 64) per the
        /// <c>is_valid_merkle_branch</c> convention in
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/ssz/merkle-proofs.md">
        /// ssz/merkle-proofs.md</see>.
        /// </summary>
        public static int CurrentSyncCommitteeBranchIndex(ConsensusFork fork) =>
            CurrentSyncCommitteeGIndex(fork) - (1 << CurrentSyncCommitteeBranchDepth(fork));

        /// <summary>
        /// Wire-format vector length for the <c>next_sync_committee_branch</c> in
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 70 (5 Altair–Deneb) +
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
        /// specs/electra/light-client/sync-protocol.md</see> line 56 (6 Electra+).
        /// </summary>
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

        /// <summary>
        /// <c>subtree_index = gindex - (1 &lt;&lt; depth)</c>. Resolves to 23 for both
        /// Altair–Deneb (55 - 32) and Electra+ (87 - 64).
        /// </summary>
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
            if (fork >= ConsensusFork.Fulu)
                throw new NotSupportedException(
                    $"Fulu/Gloas execution-branch shape not yet specified (EIP-7732 reshapes BeaconBlockBody).");
            return 4;
        }

        /// <summary>
        /// <c>subtree_index = EXECUTION_PAYLOAD_GINDEX - (1 &lt;&lt; depth) = 25 - 16 = 9</c>.
        /// </summary>
        public static int ExecutionBranchIndex(ConsensusFork fork) =>
            ExecutionPayloadGIndex - (1 << ExecutionBranchDepth(fork));

        /// <summary>
        /// True when the fork's <c>LightClientHeader</c> carries an <c>ExecutionPayloadHeader</c>
        /// per <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/capella/light-client/sync-protocol.md">
        /// specs/capella/light-client/sync-protocol.md</see>. Pre-Capella headers are a bare
        /// <c>BeaconBlockHeader</c>.
        /// </summary>
        public static bool HasExecutionPayloadHeader(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        public static bool HasWithdrawalsRoot(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        public static bool HasBlobGasFields(ConsensusFork fork) =>
            fork >= ConsensusFork.Deneb;

        private static void ThrowIfPostElectraNotImplemented(ConsensusFork fork)
        {
            if (fork >= ConsensusFork.Fulu)
                throw new NotSupportedException(
                    $"Fork {fork} light-client container shape is not yet implemented; spec follow-up tracks Fulu/Gloas.");
        }
    }
}
