using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class LightClientFinalityUpdate
    {
        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public LightClientHeader AttestedHeader { get; set; } = new LightClientHeader();
        public LightClientHeader FinalizedHeader { get; set; } = new LightClientHeader();
        public IList<byte[]> FinalityBranch { get; set; } = new List<byte[]>();
        public SyncAggregate SyncAggregate { get; set; } = new SyncAggregate();
        public ulong SignatureSlot { get; set; }

        private static int ComputeFixedSectionLength(ConsensusFork fork) =>
            (sizeof(uint) * 2) +
            SszBasicTypes.BranchByteLength(LightClientForkSpec.FinalityBranchLength(fork)) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            var attestedBytes = AttestedHeader.Encode(fork);
            var finalizedBytes = FinalizedHeader.Encode(fork);
            var aggregateBytes = SyncAggregate.Encode();
            SszBasicTypes.ValidateFixedLength(aggregateBytes, SszBasicTypes.SyncAggregateLength, nameof(SyncAggregate));

            var branchLen = LightClientForkSpec.FinalityBranchLength(fork);
            var branch = FinalityBranch as IList<byte[]> ?? new List<byte[]>(FinalityBranch);
            if (branch.Count != branchLen)
            {
                throw new InvalidOperationException(
                    $"FinalityBranch must contain {branchLen} entries for fork {fork}.");
            }

            var fixedLen = ComputeFixedSectionLength(fork);
            using var writer = new SszWriter();
            writer.WriteUInt32((uint)fixedLen);
            var finalizedOffset = fixedLen + attestedBytes.Length;
            writer.WriteUInt32((uint)finalizedOffset);
            writer.WriteFixedRootVector(branch, branchLen);
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, attestedBytes, finalizedBytes);
        }

        public static LightClientFinalityUpdate Decode(byte[] data, ConsensusFork fork)
        {
            var reader = new SszReader(data);
            var attestedOffset = reader.ReadUInt32();
            var finalizedOffset = reader.ReadUInt32();
            var finalityBranch = reader.ReadFixedRootVector(LightClientForkSpec.FinalityBranchLength(fork));
            var aggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var slot = reader.ReadUInt64();

            var fixedLen = ComputeFixedSectionLength(fork);
            ValidateOffsets(data.Length, attestedOffset, finalizedOffset, fixedLen);
            var attestedLength = (int)(finalizedOffset - attestedOffset);
            if (attestedLength <= 0)
            {
                throw new InvalidOperationException("Finalized header offset precedes attested header.");
            }

            var attestedBytes = data.AsSpan((int)attestedOffset, attestedLength).ToArray();
            var finalizedBytes = data.AsSpan((int)finalizedOffset).ToArray();

            return new LightClientFinalityUpdate
            {
                Fork = fork,
                AttestedHeader = LightClientHeader.Decode(attestedBytes, fork),
                FinalizedHeader = LightClientHeader.Decode(finalizedBytes, fork),
                FinalityBranch = finalityBranch,
                SyncAggregate = SyncAggregate.Decode(aggregateBytes),
                SignatureSlot = slot
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            return SszMerkleizer.Merkleize(new[]
            {
                AttestedHeader.HashTreeRoot(fork),
                FinalizedHeader.HashTreeRoot(fork),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(FinalityBranch)),
                SyncAggregate.HashTreeRoot(),
                SszBasicTypes.HashTreeRootUInt64(SignatureSlot)
            });
        }

        private static void ValidateOffsets(int totalLength, uint attestedOffset, uint finalizedOffset, int fixedLen)
        {
            if (attestedOffset < fixedLen || attestedOffset > totalLength)
            {
                throw new InvalidOperationException("Attested header offset exceeds buffer length.");
            }
            if (finalizedOffset < attestedOffset || finalizedOffset > totalLength)
            {
                throw new InvalidOperationException("Finalized header offset exceeds buffer length.");
            }
        }
    }
}
