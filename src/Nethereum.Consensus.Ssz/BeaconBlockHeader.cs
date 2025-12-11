using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class BeaconBlockHeader
    {
        public ulong Slot { get; set; }
        public ulong ProposerIndex { get; set; }
        public byte[] ParentRoot { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] StateRoot { get; set; } = new byte[SszBasicTypes.RootLength];
        public byte[] BodyRoot { get; set; } = new byte[SszBasicTypes.RootLength];

        public byte[] Encode()
        {
            using var writer = new SszWriter();
            writer.WriteUInt64(Slot);
            writer.WriteUInt64(ProposerIndex);
            writer.WriteFixedBytes(ParentRoot, SszBasicTypes.RootLength);
            writer.WriteFixedBytes(StateRoot, SszBasicTypes.RootLength);
            writer.WriteFixedBytes(BodyRoot, SszBasicTypes.RootLength);
            return writer.ToArray();
        }

        public static BeaconBlockHeader Decode(ReadOnlySpan<byte> data)
        {
            var reader = new SszReader(data);
            return new BeaconBlockHeader
            {
                Slot = reader.ReadUInt64(),
                ProposerIndex = reader.ReadUInt64(),
                ParentRoot = reader.ReadFixedBytes(SszBasicTypes.RootLength),
                StateRoot = reader.ReadFixedBytes(SszBasicTypes.RootLength),
                BodyRoot = reader.ReadFixedBytes(SszBasicTypes.RootLength)
            };
        }

        public byte[] HashTreeRoot()
        {
            var fieldRoots = new List<byte[]>
            {
                SszBasicTypes.HashTreeRootUInt64(Slot),
                SszBasicTypes.HashTreeRootUInt64(ProposerIndex),
                SszBasicTypes.HashTreeRootFixedBytes(ParentRoot, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(StateRoot, SszBasicTypes.RootLength),
                SszBasicTypes.HashTreeRootFixedBytes(BodyRoot, SszBasicTypes.RootLength)
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }
    }
}
