using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// <c>LightClientBootstrap</c> SSZ container per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> lines 66–74 and
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/electra/light-client/sync-protocol.md">
    /// specs/electra/light-client/sync-protocol.md</see> line 56. The
    /// <c>current_sync_committee_branch</c> length is fork-aware: 5 Altair–Deneb, 6 Electra+.
    /// </summary>
    public class LightClientBootstrap
    {
        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public LightClientHeader Header { get; set; } = new LightClientHeader();
        public SyncCommittee CurrentSyncCommittee { get; set; } = new SyncCommittee();
        public IList<byte[]> CurrentSyncCommitteeBranch { get; set; } = new List<byte[]>();

        private static int ComputeFixedSectionLength(ConsensusFork fork) =>
            sizeof(uint) +
            SszBasicTypes.SyncCommitteeLength +
            LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork) * SszBasicTypes.RootLength;

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            var headerBytes = Header.Encode(fork);
            var committeeBytes = CurrentSyncCommittee.Encode();
            SszBasicTypes.ValidateFixedLength(committeeBytes, SszBasicTypes.SyncCommitteeLength, nameof(CurrentSyncCommittee));

            var branchLen = LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork);
            var committeeBranch = CurrentSyncCommitteeBranch as IList<byte[]> ?? new List<byte[]>(CurrentSyncCommitteeBranch);
            if (committeeBranch.Count != branchLen)
            {
                throw new InvalidOperationException(
                    $"Current sync committee branch must contain {branchLen} roots for fork {fork}.");
            }

            var fixedLen = ComputeFixedSectionLength(fork);
            using var writer = new SszWriter();
            writer.WriteUInt32((uint)fixedLen);
            writer.WriteBytes(committeeBytes);
            writer.WriteFixedRootVector(committeeBranch, branchLen);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, headerBytes);
        }

        public static LightClientBootstrap Decode(byte[] data, ConsensusFork fork)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var fixedLen = ComputeFixedSectionLength(fork);
            if (data.Length < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientBootstrap buffer length {data.Length} below fixed section {fixedLen} for fork {fork}.");
            }

            var reader = new SszReader(data);
            var headerOffset = reader.ReadUInt32();
            var committeeBytes = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
            var branch = reader.ReadFixedRootVector(LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork));

            if (headerOffset < fixedLen || headerOffset > data.Length)
            {
                throw new InvalidOperationException(
                    $"Header offset {headerOffset} invalid for fork {fork} (expected {fixedLen}, buffer {data.Length}).");
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
            AssertForkConsistency(fork);

            var fieldRoots = new List<byte[]>
            {
                Header.HashTreeRoot(fork),
                CurrentSyncCommittee.HashTreeRoot(),
                SszBasicTypes.HashTreeRootBranch(new List<byte[]>(CurrentSyncCommitteeBranch))
            };

            return SszMerkleizer.Merkleize(fieldRoots);
        }

        private void AssertForkConsistency(ConsensusFork fork)
        {
            if (Header != null && Header.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientBootstrap.Header.Fork={Header.Fork} but outer fork is {fork}; nested containers must share fork.");
            }
        }
    }
}
