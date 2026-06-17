using System;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class LightClientOptimisticUpdate
    {
        private const int FixedSectionLength =
            sizeof(uint) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        public ConsensusFork Fork { get; set; } = ConsensusFork.Electra;

        public LightClientHeader AttestedHeader { get; set; } = new LightClientHeader();
        public SyncAggregate SyncAggregate { get; set; } = new SyncAggregate();
        public ulong SignatureSlot { get; set; }

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            var headerBytes = AttestedHeader.Encode(fork);
            var aggregateBytes = SyncAggregate.Encode();
            SszBasicTypes.ValidateFixedLength(aggregateBytes, SszBasicTypes.SyncAggregateLength, nameof(SyncAggregate));

            using var writer = new SszWriter();
            writer.WriteUInt32((uint)FixedSectionLength);
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, headerBytes);
        }

        public static LightClientOptimisticUpdate Decode(byte[] data, ConsensusFork fork)
        {
            var reader = new SszReader(data);
            var headerOffset = reader.ReadUInt32();
            var aggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var slot = reader.ReadUInt64();

            if (headerOffset < FixedSectionLength || headerOffset > data.Length)
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
            return SszMerkleizer.Merkleize(new[]
            {
                AttestedHeader.HashTreeRoot(fork),
                SyncAggregate.HashTreeRoot(),
                SszBasicTypes.HashTreeRootUInt64(SignatureSlot)
            });
        }
    }
}
