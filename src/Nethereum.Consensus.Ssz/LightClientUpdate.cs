using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
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
            SszBasicTypes.BranchByteLength(LightClientForkSpec.CurrentSyncCommitteeBranchLength) +
            sizeof(uint) +
            SszBasicTypes.BranchByteLength(LightClientForkSpec.FinalityBranchLength(fork)) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            var attestedBytes = AttestedHeader.Encode(fork);
            var nextCommitteeBytes = NextSyncCommittee.Encode();
            SszBasicTypes.ValidateFixedLength(nextCommitteeBytes, SszBasicTypes.SyncCommitteeLength, nameof(NextSyncCommittee));

            var finalizedBytes = FinalizedHeader.Encode(fork);
            var aggregateBytes = SyncAggregate.Encode();
            SszBasicTypes.ValidateFixedLength(aggregateBytes, SszBasicTypes.SyncAggregateLength, nameof(SyncAggregate));

            var nextBranch = NextSyncCommitteeBranch as IList<byte[]> ?? new List<byte[]>(NextSyncCommitteeBranch);
            var finalityBranch = FinalityBranch as IList<byte[]> ?? new List<byte[]>(FinalityBranch);
            ValidateBranchCount(nextBranch, LightClientForkSpec.CurrentSyncCommitteeBranchLength, nameof(NextSyncCommitteeBranch));
            ValidateBranchCount(finalityBranch, LightClientForkSpec.FinalityBranchLength(fork), nameof(FinalityBranch));

            var fixedLen = ComputeFixedSectionLength(fork);
            var attestedOffset = fixedLen;
            var finalizedOffset = attestedOffset + attestedBytes.Length;

            using var writer = new SszWriter();
            writer.WriteUInt32((uint)attestedOffset);
            writer.WriteBytes(nextCommitteeBytes);
            writer.WriteFixedRootVector(nextBranch, LightClientForkSpec.CurrentSyncCommitteeBranchLength);
            writer.WriteUInt32((uint)finalizedOffset);
            writer.WriteFixedRootVector(finalityBranch, LightClientForkSpec.FinalityBranchLength(fork));
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, attestedBytes, finalizedBytes);
        }

        public static LightClientUpdate Decode(byte[] data, ConsensusFork fork)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var reader = new SszReader(data);
            var attestedOffset = reader.ReadUInt32();
            var nextSyncCommitteeBytes = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
            var nextBranch = reader.ReadFixedRootVector(LightClientForkSpec.CurrentSyncCommitteeBranchLength);
            var finalizedOffset = reader.ReadUInt32();
            var finalityBranch = reader.ReadFixedRootVector(LightClientForkSpec.FinalityBranchLength(fork));
            var syncAggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var signatureSlot = reader.ReadUInt64();

            var fixedLen = ComputeFixedSectionLength(fork);
            ValidateOffset(data.Length, attestedOffset, "attested_header", fixedLen);
            ValidateOffset(data.Length, finalizedOffset, "finalized_header", fixedLen);
            if (finalizedOffset < attestedOffset)
            {
                throw new InvalidOperationException("Finalized header offset precedes attested header offset.");
            }

            var attestedLength = (int)(finalizedOffset - attestedOffset);
            if (attestedLength <= 0)
            {
                throw new InvalidOperationException("Finalized header offset must be greater than attested header offset.");
            }

            var attestedBytes = data.AsSpan((int)attestedOffset, attestedLength).ToArray();
            var finalizedBytes = data.AsSpan((int)finalizedOffset).ToArray();

            return new LightClientUpdate
            {
                Fork = fork,
                AttestedHeader = LightClientHeader.Decode(attestedBytes, fork),
                NextSyncCommittee = SyncCommittee.Decode(nextSyncCommitteeBytes),
                NextSyncCommitteeBranch = nextBranch,
                FinalizedHeader = LightClientHeader.Decode(finalizedBytes, fork),
                FinalityBranch = finalityBranch,
                SyncAggregate = SyncAggregate.Decode(syncAggregateBytes),
                SignatureSlot = signatureSlot
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
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
            if (offset < fixedLen || offset > totalLength)
            {
                throw new InvalidOperationException($"{label} offset {offset} exceeds bounds [{fixedLen}, {totalLength}].");
            }
        }
    }
}
