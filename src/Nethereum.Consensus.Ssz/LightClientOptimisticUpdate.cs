using System;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// <c>LightClientOptimisticUpdate</c> SSZ container per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see>. At Altair/Bellatrix the
    /// <c>attested_header</c> field is a fixed-size 112-byte beacon header inlined directly into
    /// the fixed section (no offset). At Capella+ the header becomes variable-size and is
    /// offset-encoded.
    /// </summary>
    public class LightClientOptimisticUpdate
    {
        private static int ComputeFixedSectionLength(ConsensusFork fork)
        {
            var headerSlotBytes = LightClientForkSpec.HasExecutionPayloadHeader(fork)
                ? sizeof(uint)
                : SszBasicTypes.BeaconBlockHeaderLength;
            return headerSlotBytes +
                   SszBasicTypes.SyncAggregateLength +
                   sizeof(ulong);
        }

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

            using var writer = new SszWriter();
            if (LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                var fixedLen = ComputeFixedSectionLength(fork);
                writer.WriteUInt32((uint)fixedLen);
                writer.WriteBytes(aggregateBytes);
                writer.WriteUInt64(SignatureSlot);

                var fixedSection = writer.ToArray();
                return SszContainerEncoding.Combine(fixedSection, headerBytes);
            }

            SszBasicTypes.ValidateFixedLength(headerBytes, SszBasicTypes.BeaconBlockHeaderLength, nameof(AttestedHeader));
            writer.WriteBytes(headerBytes);
            writer.WriteBytes(aggregateBytes);
            writer.WriteUInt64(SignatureSlot);
            return writer.ToArray();
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

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                var attestedInline = reader.ReadFixedBytes(SszBasicTypes.BeaconBlockHeaderLength);
                var aggregateBytesAlt = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
                var slotAlt = reader.ReadUInt64();

                return new LightClientOptimisticUpdate
                {
                    Fork = fork,
                    AttestedHeader = LightClientHeader.Decode(attestedInline, fork),
                    SyncAggregate = SyncAggregate.Decode(aggregateBytesAlt),
                    SignatureSlot = slotAlt
                };
            }

            var headerOffset = reader.ReadUInt32();
            var aggregateBytes = reader.ReadFixedBytes(SszBasicTypes.SyncAggregateLength);
            var slot = reader.ReadUInt64();

            if (headerOffset < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientOptimisticUpdate: attested header offset {headerOffset} precedes fixed section length {fixedLen}.");
            }
            if (headerOffset > data.Length)
            {
                throw new InvalidOperationException(
                    $"LightClientOptimisticUpdate: attested header offset {headerOffset} exceeds buffer length {data.Length}.");
            }

            var headerBytes = data.AsSpan((int)headerOffset).ToArray();

            LightClientHeader attestedHeader;
            try
            {
                attestedHeader = LightClientHeader.Decode(headerBytes, fork);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"LightClientOptimisticUpdate: inner header decode failed for fork {fork}: {ex.Message}", ex);
            }

            return new LightClientOptimisticUpdate
            {
                Fork = fork,
                AttestedHeader = attestedHeader,
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
