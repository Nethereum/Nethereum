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
    /// Fulu inherits all LightClient gindex / depth values from Electra: the Fulu spec
    /// tree at <see href="https://github.com/ethereum/consensus-specs/tree/master/specs/fulu">
    /// specs/fulu</see> contains no <c>light-client/</c> subdirectory, and Fulu's only
    /// <c>BeaconState</c> change is appending <c>proposer_lookahead</c> (38th field) per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/fulu/beacon-chain.md">
    /// specs/fulu/beacon-chain.md</see> line 188. The container's tree depth stays at 6
    /// (next power of two ≥ 38 is 64 = 2^6, same as Electra's 37 fields), so the existing
    /// field gindices — including <c>finalized_checkpoint.root</c>, <c>current_sync_committee</c>,
    /// and <c>next_sync_committee</c> — are preserved. Cross-validated against Lighthouse
    /// <c>LightClientHeaderFulu</c> (which carries the same merkle branch lengths as the
    /// Electra variant) and Lodestar (which reuses Electra <c>ExecutionPayload</c> verbatim
    /// in <c>packages/types/src/fulu/sszTypes.ts</c>).
    /// </summary>
    public static class LightClientForkSpec
    {
        /// <summary>
        /// <c>MIN_SYNC_COMMITTEE_PARTICIPANTS = 1</c> per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/presets/mainnet/altair.yaml">
        /// presets/mainnet/altair.yaml</see> line 22 and
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
        /// specs/altair/light-client/sync-protocol.md</see> line 82. Constant from Altair
        /// through Electra — no fork-aware variation.
        /// </summary>
        public const int MinSyncCommitteeParticipants = 1;

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
            if (fork >= ConsensusFork.Gloas)
                throw new NotSupportedException(
                    $"Gloas execution-branch shape not yet specified (EIP-7732 reshapes BeaconBlockBody).");
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
        /// <c>BeaconBlockHeader</c>. This predicate also controls whether <c>LightClientHeader</c>
        /// is encoded as a variable-size SSZ container (Capella+, because the embedded
        /// <c>ExecutionPayloadHeader.extra_data</c> is variable-length) versus a fixed-size
        /// 112-byte beacon header inlined directly into outer containers (Altair/Bellatrix).
        /// </summary>
        public static bool HasExecutionPayloadHeader(ConsensusFork fork) =>
            fork >= ConsensusFork.Capella;

        /// <summary>
        /// True when the fork has an <c>ExecutionPayloadHeader</c> SSZ container defined,
        /// independent of whether the LightClient surface area uses it. The container is
        /// introduced at Bellatrix per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/bellatrix/beacon-chain.md">
        /// specs/bellatrix/beacon-chain.md</see> as part of The Merge; at Capella the
        /// <c>LightClientHeader</c> begins to embed it per
        /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/capella/light-client/sync-protocol.md">
        /// specs/capella/light-client/sync-protocol.md</see>. Distinct from
        /// <see cref="HasExecutionPayloadHeader"/>, which gates the LightClientHeader.execution field.
        /// </summary>
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
