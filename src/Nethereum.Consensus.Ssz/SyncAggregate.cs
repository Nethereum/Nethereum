using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
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
