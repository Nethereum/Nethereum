using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// <c>LightClientUpdate</c> SSZ container per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 75–83 and
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
    /// specs/electra/light-client/sync-protocol.md</see> line 56. Both
    /// <c>next_sync_committee_branch</c> and <c>finality_branch</c> are fork-aware:
    /// next-committee branch depth 5 Altair–Deneb / 6 Electra+; finality branch depth 6 / 7.
    /// </summary>
    public class LightClientUpdate
    {
        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public LightClientHeader AttestedHeader { get; set; } = new LightClientHeader();
        public SyncCommittee NextSyncCommittee { get; set; } = new SyncCommittee();
        public IList<byte[]> NextSyncCommitteeBranch { get; set; } = new List<byte[]>();
        public LightClientHeader FinalizedHeader { get; set; } = new LightClientHeader();
        public IList<byte[]> FinalityBranch { get; set; } = new List<byte[]>();
        public SyncAggregate SyncAggregate { get; set; } = new SyncAggregate();
        public ulong SignatureSlot { get; set; }

        private static int ComputeFixedSectionLength(ConsensusFork fork) =>
            sizeof(uint) +
            SszBasicTypes.SyncCommitteeLength +
            SszBasicTypes.BranchByteLength(LightClientForkSpec.NextSyncCommitteeBranchLength(fork)) +
            sizeof(uint) +
            SszBasicTypes.BranchByteLength(LightClientForkSpec.FinalityBranchLength(fork)) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            var attestedBytes = AttestedHeader.Encode(fork);
            var nextCommitteeBytes = NextSyncCommittee.Encode();
            SszBasicTypes.ValidateFixedLength(nextCommitteeBytes, SszBasicTypes.SyncCommitteeLength, nameof(NextSyncCommittee));

            var finalizedBytes = FinalizedHeader.Encode(fork);
            var aggregateBytes = SyncAggregate.Encode();
            SszBasicTypes.ValidateFixedLength(aggregateBytes, SszBasicTypes.SyncAggregateLength, nameof(SyncAggregate));

            var nextBranchLen = LightClientForkSpec.NextSyncCommitteeBranchLength(fork);
            var finalityBranchLen = LightClientForkSpec.FinalityBranchLength(fork);
            var nextBranch = NextSyncCommitteeBranch as IList<byte[]> ?? new List<byte[]>(NextSyncCommitteeBranch);
            var finalityBranch = FinalityBranch as IList<byte[]> ?? new List<byte[]>(FinalityBranch);
            ValidateBranchCount(nextBranch, nextBranchLen, nameof(NextSyncCommitteeBranch));
            ValidateBranchCount(finalityBranch, finalityBranchLen, nameof(FinalityBranch));

            var fixedLen = ComputeFixedSectionLength(fork);
            var attestedOffset = fixedLen;
            var finalizedOffset = attestedOffset + attestedBytes.Length;

            using var writer = new SszWriter();
            writer.WriteUInt32((uint)attestedOffset);
            writer.WriteBytes(nextCommitteeBytes);
            writer.WriteFixedRootVector(nextBranch, nextBranchLen);
            writer.WriteUInt32((uint)finalizedOffset);
            writer.WriteFixedRootVector(finalityBranch, finalityBranchLen);
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, attestedBytes, finalizedBytes);
        }

        public static LightClientUpdate Decode(byte[] data, ConsensusFork fork)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var fixedLen = ComputeFixedSectionLength(fork);
            if (data.Length < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientUpdate: input length {data.Length} shorter than fixed section {fixedLen} for fork {fork}.");
            }

            var reader = new SszReader(data);
            var attestedOffset = reader.ReadUInt32();
            var nextSyncCommitteeBytes = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
            var nextBranch = reader.ReadFixedRootVector(LightClientForkSpec.NextSyncCommitteeBranchLength(fork));
            var finalizedOffset = reader.ReadUInt32();
            var finalityBranch = reader.ReadFixedRootVector(LightClientForkSpec.FinalityBranchLength(fork));
            var syncAggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var signatureSlot = reader.ReadUInt64();

            ValidateOffset(data.Length, attestedOffset, "attested_header", fixedLen);
            ValidateOffset(data.Length, finalizedOffset, "finalized_header", fixedLen);
            if (finalizedOffset < attestedOffset)
            {
                throw new InvalidOperationException(
                    $"LightClientUpdate: finalized_header offset {finalizedOffset} precedes attested_header offset {attestedOffset}.");
            }

            var attestedLength = (int)(finalizedOffset - attestedOffset);
            var minHeaderSize = MinHeaderSize(fork);
            if (attestedLength < minHeaderSize)
            {
                throw new InvalidOperationException(
                    $"LightClientUpdate: attested_header length {attestedLength} below minimum {minHeaderSize} for fork {fork}.");
            }

            var attestedBytes = data.AsSpan((int)attestedOffset, attestedLength).ToArray();
            var finalizedBytes = data.AsSpan((int)finalizedOffset).ToArray();

            LightClientHeader attestedHeader;
            LightClientHeader finalizedHeader;
            try
            {
                attestedHeader = LightClientHeader.Decode(attestedBytes, fork);
                finalizedHeader = LightClientHeader.Decode(finalizedBytes, fork);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"LightClientUpdate: inner header decode failed for fork {fork}: {ex.Message}", ex);
            }

            return new LightClientUpdate
            {
                Fork = fork,
                AttestedHeader = attestedHeader,
                NextSyncCommittee = SyncCommittee.Decode(nextSyncCommitteeBytes),
                NextSyncCommitteeBranch = nextBranch,
                FinalizedHeader = finalizedHeader,
                FinalityBranch = finalityBranch,
                SyncAggregate = SyncAggregate.Decode(syncAggregateBytes),
                SignatureSlot = signatureSlot
            };
        }

        private static int MinHeaderSize(ConsensusFork fork) =>
            LightClientForkSpec.HasExecutionPayloadHeader(fork)
                ? SszBasicTypes.BeaconBlockHeaderLength +
                  sizeof(uint) +
                  SszBasicTypes.BranchByteLength(LightClientForkSpec.ExecutionBranchDepth(fork))
                : SszBasicTypes.BeaconBlockHeaderLength;

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            var fieldRoots = new List<byte[]>
            {
                AttestedHeader.HashTreeRoot(fork),
                NextSyncCommittee.HashTreeRoot(),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(NextSyncCommitteeBranch)),
                FinalizedHeader.HashTreeRoot(fork),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(FinalityBranch)),
                SyncAggregate.HashTreeRoot(),
                SszBasicTypes.HashTreeRootUInt64(SignatureSlot)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }

        private static void ValidateBranchCount(IList<byte[]> branch, int expectedCount, string name)
        {
            if (branch.Count != expectedCount)
            {
                throw new InvalidOperationException($"{name} must contain {expectedCount} entries.");
            }
        }

        private static void ValidateOffset(int totalLength, uint offset, string label, int fixedLen)
        {
            if (offset < fixedLen)
            {
                throw new InvalidOperationException(
                    $"{label} offset {offset} precedes fixed section length {fixedLen}.");
            }
            if (offset > totalLength)
            {
                throw new InvalidOperationException(
                    $"{label} offset {offset} exceeds SSZ buffer length {totalLength}.");
            }
        }

        private void AssertForkConsistency(ConsensusFork fork)
        {
            if (AttestedHeader != null && AttestedHeader.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientUpdate.AttestedHeader.Fork={AttestedHeader.Fork} but outer fork is {fork}.");
            }
            if (FinalizedHeader != null && FinalizedHeader.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientUpdate.FinalizedHeader.Fork={FinalizedHeader.Fork} but outer fork is {fork}.");
            }
        }
    }
}
