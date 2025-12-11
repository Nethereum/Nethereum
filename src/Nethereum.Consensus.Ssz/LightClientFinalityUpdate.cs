using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class LightClientFinalityUpdate
    {
        private static readonly int FixedSectionLength =
            (sizeof(uint) * 2) +
            SszBasicTypes.BranchByteLength(SszBasicTypes.FinalityBranchLength) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        public LightClientHeader AttestedHeader { get; set; } = new LightClientHeader();
        public LightClientHeader FinalizedHeader { get; set; } = new LightClientHeader();
        public IList<byte[]> FinalityBranch { get; set; } = new List<byte[]>();
        public SyncAggregate SyncAggregate { get; set; } = new SyncAggregate();
        public ulong SignatureSlot { get; set; }

        public byte[] Encode()
        {
            var attestedBytes = AttestedHeader.Encode();
            var finalizedBytes = FinalizedHeader.Encode();
            var aggregateBytes = SyncAggregate.Encode();
            SszBasicTypes.ValidateFixedLength(aggregateBytes, SszBasicTypes.SyncAggregateLength, nameof(SyncAggregate));

            var branch = FinalityBranch as IList<byte[]> ?? new List<byte[]>(FinalityBranch);

            using var writer = new SszWriter();
            writer.WriteUInt32((uint)FixedSectionLength);
            var finalizedOffset = FixedSectionLength + attestedBytes.Length;
            writer.WriteUInt32((uint)finalizedOffset);
            writer.WriteFixedRootVector(branch, SszBasicTypes.FinalityBranchLength);
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, attestedBytes, finalizedBytes);
        }

        public static LightClientFinalityUpdate Decode(byte[] data)
        {
            var reader = new SszReader(data);
            var attestedOffset = reader.ReadUInt32();
            var finalizedOffset = reader.ReadUInt32();
            var finalityBranch = reader.ReadFixedRootVector(SszBasicTypes.FinalityBranchLength);
            var aggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var slot = reader.ReadUInt64();

            ValidateOffsets(data.Length, attestedOffset, finalizedOffset);
            var attestedLength = (int)(finalizedOffset - attestedOffset);
            if (attestedLength <= 0)
            {
                throw new InvalidOperationException("Finalized header offset precedes attested header.");
            }

            var attestedBytes = data.AsSpan((int)attestedOffset, attestedLength).ToArray();
            var finalizedBytes = data.AsSpan((int)finalizedOffset).ToArray();

            return new LightClientFinalityUpdate
            {
                AttestedHeader = LightClientHeader.Decode(attestedBytes),
                FinalizedHeader = LightClientHeader.Decode(finalizedBytes),
                FinalityBranch = finalityBranch,
                SyncAggregate = SyncAggregate.Decode(aggregateBytes),
                SignatureSlot = slot
            };
        }

        public byte[] HashTreeRoot()
        {
            return SszMerkleizer.Merkleize(new[]
            {
                AttestedHeader.HashTreeRoot(),
                FinalizedHeader.HashTreeRoot(),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(FinalityBranch)),
                SyncAggregate.HashTreeRoot(),
                SszBasicTypes.HashTreeRootUInt64(SignatureSlot)
            });
        }

        private static void ValidateOffsets(int totalLength, uint attestedOffset, uint finalizedOffset)
        {
            if (attestedOffset < FixedSectionLength || attestedOffset > totalLength)
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
