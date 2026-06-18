using System;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// <c>LightClientOptimisticUpdate</c> SSZ container per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see>. Fixed section is the AttestedHeader
    /// offset (4 bytes) + SyncAggregate (160 bytes) + SignatureSlot (8 bytes) — constant
    /// across Altair–Electra (172 bytes total). The fork parameter is threaded through so the
    /// AttestedHeader can be decoded with its fork-specific shape.
    /// </summary>
    public class LightClientOptimisticUpdate
    {
        private static int ComputeFixedSectionLength(ConsensusFork _) =>
            sizeof(uint) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public LightClientHeader AttestedHeader { get; set; } = new LightClientHeader();
        public SyncAggregate SyncAggregate { get; set; } = new SyncAggregate();
        public ulong SignatureSlot { get; set; }

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            var headerBytes = AttestedHeader.Encode(fork);
            var aggregateBytes = SyncAggregate.Encode();
            SszBasicTypes.ValidateFixedLength(aggregateBytes, SszBasicTypes.SyncAggregateLength, nameof(SyncAggregate));

            var fixedLen = ComputeFixedSectionLength(fork);
            using var writer = new SszWriter();
            writer.WriteUInt32((uint)fixedLen);
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, headerBytes);
        }

        public static LightClientOptimisticUpdate Decode(byte[] data, ConsensusFork fork)
        {
            var fixedLen = ComputeFixedSectionLength(fork);
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientOptimisticUpdate buffer length {data.Length} below fixed section {fixedLen} for fork {fork}.");
            }

            var reader = new SszReader(data);
            var headerOffset = reader.ReadUInt32();
            var aggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var slot = reader.ReadUInt64();

            if (headerOffset < fixedLen || headerOffset > data.Length)
            {
                throw new InvalidOperationException("Attested header offset exceeds buffer length.");
            }

            var headerBytes = data.AsSpan((int)headerOffset).ToArray();

            return new LightClientOptimisticUpdate
            {
                Fork = fork,
                AttestedHeader = LightClientHeader.Decode(headerBytes, fork),
                SyncAggregate = SyncAggregate.Decode(aggregateBytes),
                SignatureSlot = slot
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            return SszMerkleizer.Merkleize(new[]
            {
                AttestedHeader.HashTreeRoot(fork),
                SyncAggregate.HashTreeRoot(),
                SszBasicTypes.HashTreeRootUInt64(SignatureSlot)
            });
        }

        private void AssertForkConsistency(ConsensusFork fork)
        {
            if (AttestedHeader != null && AttestedHeader.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientOptimisticUpdate.AttestedHeader.Fork={AttestedHeader.Fork} but outer fork is {fork}.");
            }
        }
    }
}
