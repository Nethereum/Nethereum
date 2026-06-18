using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// <c>SyncAggregate</c> SSZ container per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/beacon-chain.md">
    /// specs/altair/beacon-chain.md</see>. <c>sync_committee_bits</c> is a
    /// <c>Bitvector[SYNC_COMMITTEE_SIZE]</c>; per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/ssz/simple-serialize.md">
    /// ssz/simple-serialize.md</see> Bitvector serialisation, a <c>Bitvector[N]</c> with
    /// <c>N % 8 == 0</c> serialises to exactly <c>N / 8</c> bytes with no trailing
    /// zero-bits mask. With <c>SYNC_COMMITTEE_SIZE = 512</c> the bitvector occupies
    /// 64 bytes exactly and the total <c>SyncAggregate</c> length is 160 bytes
    /// (64 bits + 96 signature). The decoder enforces this exact length at the
    /// container layer.
    /// </summary>
    public class SyncAggregate
    {
        public byte[] SyncCommitteeBits { get; set; } = new byte[SszBasicTypes.SyncCommitteeSize / 8]; // expected 64 bytes (512 bits)
        public byte[] SyncCommitteeSignature { get; set; } = new byte[SszBasicTypes.SignatureLength];

        public byte[] Encode()
        {
            using var writer = new SszWriter();
            writer.WriteFixedBytes(SyncCommitteeBits ?? Array.Empty<byte>(), SszBasicTypes.SyncCommitteeSize / 8);
            writer.WriteFixedBytes(SyncCommitteeSignature, SszBasicTypes.SignatureLength);
            return writer.ToArray();
        }

        public static SyncAggregate Decode(ReadOnlySpan<byte> data)
        {
            if (data.Length != SszBasicTypes.SyncAggregateLength)
            {
                throw new InvalidOperationException(
                    $"SyncAggregate: input length {data.Length} must equal {SszBasicTypes.SyncAggregateLength} bytes.");
            }

            var reader = new SszReader(data);
            return new SyncAggregate
            {
                SyncCommitteeBits = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeSize / 8),
                SyncCommitteeSignature = reader.ReadFixedBytes(SszBasicTypes.SignatureLength)
            };
        }

        public byte[] HashTreeRoot()
        {
            var fieldRoots = new List<byte[]>
            {
                SszBasicTypes.HashTreeRootFixedBytes(SyncCommitteeBits ?? Array.Empty<byte>(), SszBasicTypes.SyncCommitteeSize / 8),
                SszBasicTypes.HashTreeRootFixedBytes(SyncCommitteeSignature, SszBasicTypes.SignatureLength)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }
    }
}
