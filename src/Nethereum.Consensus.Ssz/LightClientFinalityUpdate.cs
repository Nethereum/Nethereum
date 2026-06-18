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
            AssertForkConsistency(fork);

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
            if (data == null) throw new ArgumentNullException(nameof(data));

            var fixedLen = ComputeFixedSectionLength(fork);
            if (data.Length < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientFinalityUpdate: input length {data.Length} shorter than fixed section {fixedLen} for fork {fork}.");
            }

            var reader = new SszReader(data);
            var attestedOffset = reader.ReadUInt32();
            var finalizedOffset = reader.ReadUInt32();
            var finalityBranch = reader.ReadFixedRootVector(LightClientForkSpec.FinalityBranchLength(fork));
            var aggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var slot = reader.ReadUInt64();

            ValidateOffsets(data.Length, attestedOffset, finalizedOffset, fixedLen);
            var attestedLength = (int)(finalizedOffset - attestedOffset);
            var minHeaderSize = MinHeaderSize(fork);
            if (attestedLength < minHeaderSize)
            {
                throw new InvalidOperationException(
                    $"LightClientFinalityUpdate: attested_header length {attestedLength} below minimum {minHeaderSize} for fork {fork}.");
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
                    $"LightClientFinalityUpdate: inner header decode failed for fork {fork}: {ex.Message}", ex);
            }

            return new LightClientFinalityUpdate
            {
                Fork = fork,
                AttestedHeader = attestedHeader,
                FinalizedHeader = finalizedHeader,
                FinalityBranch = finalityBranch,
                SyncAggregate = SyncAggregate.Decode(aggregateBytes),
                SignatureSlot = slot
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
            if (attestedOffset < fixedLen)
            {
                throw new InvalidOperationException(
                    $"Attested header offset {attestedOffset} precedes fixed section length {fixedLen}.");
            }
            if (attestedOffset > totalLength)
            {
                throw new InvalidOperationException(
                    $"Attested header offset {attestedOffset} exceeds SSZ buffer length {totalLength}.");
            }
            if (finalizedOffset < attestedOffset)
            {
                throw new InvalidOperationException(
                    $"Finalized header offset {finalizedOffset} precedes attested header offset {attestedOffset}.");
            }
            if (finalizedOffset > totalLength)
            {
                throw new InvalidOperationException(
                    $"Finalized header offset {finalizedOffset} exceeds SSZ buffer length {totalLength}.");
            }
        }

        private void AssertForkConsistency(ConsensusFork fork)
        {
            if (AttestedHeader != null && AttestedHeader.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientFinalityUpdate.AttestedHeader.Fork={AttestedHeader.Fork} but outer fork is {fork}.");
            }
            if (FinalizedHeader != null && FinalizedHeader.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientFinalityUpdate.FinalizedHeader.Fork={FinalizedHeader.Fork} but outer fork is {fork}.");
            }
        }
    }
}
