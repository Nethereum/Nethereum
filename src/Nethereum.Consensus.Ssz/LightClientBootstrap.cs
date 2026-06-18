using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class LightClientBootstrap
    {
        private const int FixedSectionLength =
            sizeof(uint) +
            SszBasicTypes.SyncCommitteeLength +
            LightClientForkSpec.CurrentSyncCommitteeBranchLength * SszBasicTypes.RootLength;

        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public LightClientHeader Header { get; set; } = new LightClientHeader();
        public SyncCommittee CurrentSyncCommittee { get; set; } = new SyncCommittee();
        public IList<byte[]> CurrentSyncCommitteeBranch { get; set; } = new List<byte[]>();

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            var headerBytes = Header.Encode(fork);
            var committeeBytes = CurrentSyncCommittee.Encode();
            SszBasicTypes.ValidateFixedLength(committeeBytes, SszBasicTypes.SyncCommitteeLength, nameof(CurrentSyncCommittee));

            var committeeBranch = CurrentSyncCommitteeBranch as IList<byte[]> ?? new List<byte[]>(CurrentSyncCommitteeBranch);
            if (committeeBranch.Count != LightClientForkSpec.CurrentSyncCommitteeBranchLength)
            {
                throw new InvalidOperationException(
                    $"Current sync committee branch must contain {LightClientForkSpec.CurrentSyncCommitteeBranchLength} roots.");
            }

            using var writer = new SszWriter();
            writer.WriteUInt32((uint)FixedSectionLength);
            writer.WriteBytes(committeeBytes);
            writer.WriteFixedRootVector(committeeBranch, LightClientForkSpec.CurrentSyncCommitteeBranchLength);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, headerBytes);
        }

        public static LightClientBootstrap Decode(byte[] data, ConsensusFork fork)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var reader = new SszReader(data);
            var headerOffset = reader.ReadUInt32();
            var committeeBytes = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
            var branch = reader.ReadFixedRootVector(LightClientForkSpec.CurrentSyncCommitteeBranchLength);

            if (headerOffset < FixedSectionLength || headerOffset > data.Length)
            {
                throw new InvalidOperationException("Header offset exceeds buffer length.");
            }

            var headerBytes = data.AsSpan((int)headerOffset).ToArray();

            return new LightClientBootstrap
            {
                Fork = fork,
                Header = LightClientHeader.Decode(headerBytes, fork),
                CurrentSyncCommittee = SyncCommittee.Decode(committeeBytes),
                CurrentSyncCommitteeBranch = branch
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            var fieldRoots = new List<byte[]>
            {
                Header.HashTreeRoot(fork),
                CurrentSyncCommittee.HashTreeRoot(),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(CurrentSyncCommitteeBranch))
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }
    }
}
