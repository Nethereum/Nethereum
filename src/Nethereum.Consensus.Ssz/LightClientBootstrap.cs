using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class LightClientBootstrap
    {
        private static readonly int FixedSectionLength =
            sizeof(uint) +
            SszBasicTypes.SyncCommitteeLength +
            SszBasicTypes.BranchByteLength(SszBasicTypes.CurrentSyncCommitteeBranchLength);

        public LightClientHeader Header { get; set; } = new LightClientHeader();
        public SyncCommittee CurrentSyncCommittee { get; set; } = new SyncCommittee();
        public IList<byte[]> CurrentSyncCommitteeBranch { get; set; } = new List<byte[]>();

        public byte[] Encode()
        {
            var headerBytes = Header.Encode();
            var committeeBytes = CurrentSyncCommittee.Encode();
            SszBasicTypes.ValidateFixedLength(committeeBytes, SszBasicTypes.SyncCommitteeLength, nameof(CurrentSyncCommittee));

            var committeeBranch = CurrentSyncCommitteeBranch as IList<byte[]> ?? new List<byte[]>(CurrentSyncCommitteeBranch);
            if (committeeBranch.Count != SszBasicTypes.CurrentSyncCommitteeBranchLength)
            {
                throw new InvalidOperationException($"Current sync committee branch must contain {SszBasicTypes.CurrentSyncCommitteeBranchLength} roots.");
            }

            using var writer = new SszWriter();
            writer.WriteUInt32((uint)FixedSectionLength);
            writer.WriteBytes(committeeBytes);
            writer.WriteFixedRootVector(committeeBranch, SszBasicTypes.CurrentSyncCommitteeBranchLength);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, headerBytes);
        }

        public static LightClientBootstrap Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var reader = new SszReader(data);
            var headerOffset = reader.ReadUInt32();
            var committeeBytes = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
            var branch = reader.ReadFixedRootVector(SszBasicTypes.CurrentSyncCommitteeBranchLength);

            if (headerOffset < FixedSectionLength || headerOffset > data.Length)
            {
                throw new InvalidOperationException("Header offset exceeds buffer length.");
            }

            var headerSpan = data.AsSpan((int)headerOffset);

            return new LightClientBootstrap
            {
                Header = LightClientHeader.Decode(headerSpan.ToArray()),
                CurrentSyncCommittee = SyncCommittee.Decode(committeeBytes),
                CurrentSyncCommitteeBranch = branch
            };
        }

        public byte[] HashTreeRoot()
        {
            var fieldRoots = new List<byte[]>
            {
                Header.HashTreeRoot(),
                CurrentSyncCommittee.HashTreeRoot(),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(CurrentSyncCommitteeBranch))
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }
    }
}
