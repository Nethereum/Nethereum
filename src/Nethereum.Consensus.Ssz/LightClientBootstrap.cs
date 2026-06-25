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
    /// At Altair/Bellatrix the <c>header</c> field is the fixed-size 112-byte
    /// <c>BeaconBlockHeader</c> and is inlined into the fixed section per SSZ rules. At Capella+
    /// the header becomes variable-size (<c>ExecutionPayloadHeader.extra_data</c>) and is
    /// offset-encoded.
    /// </summary>
    public class LightClientBootstrap
    {
        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public LightClientHeader Header { get; set; } = new LightClientHeader();
        public SyncCommittee CurrentSyncCommittee { get; set; } = new SyncCommittee();
        public IList<byte[]> CurrentSyncCommitteeBranch { get; set; } = new List<byte[]>();

        private static int ComputeFixedSectionLength(ConsensusFork fork)
        {
            var headerBytes = LightClientForkSpec.HasExecutionPayloadHeader(fork)
                ? sizeof(uint)
                : SszBasicTypes.BeaconBlockHeaderLength;
            return headerBytes +
                   SszBasicTypes.SyncCommitteeLength +
                   LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork) * SszBasicTypes.RootLength;
        }

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

            using var writer = new SszWriter();
            if (LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                var fixedLen = ComputeFixedSectionLength(fork);
                writer.WriteUInt32((uint)fixedLen);
                writer.WriteBytes(committeeBytes);
                writer.WriteFixedRootVector(committeeBranch, branchLen);
                var fixedSection = writer.ToArray();
                return SszContainerEncoding.Combine(fixedSection, headerBytes);
            }

            SszBasicTypes.ValidateFixedLength(headerBytes, SszBasicTypes.BeaconBlockHeaderLength, nameof(Header));
            writer.WriteBytes(headerBytes);
            writer.WriteBytes(committeeBytes);
            writer.WriteFixedRootVector(committeeBranch, branchLen);
            return writer.ToArray();
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

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                var headerBytes = reader.ReadFixedBytes(SszBasicTypes.BeaconBlockHeaderLength);
                var committeeBytes = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
                var branch = reader.ReadFixedRootVector(LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork));
                return new LightClientBootstrap
                {
                    Fork = fork,
                    Header = LightClientHeader.Decode(headerBytes, fork),
                    CurrentSyncCommittee = SyncCommittee.Decode(committeeBytes),
                    CurrentSyncCommitteeBranch = branch
                };
            }

            var headerOffset = reader.ReadUInt32();
            var committeeBytesPost = reader.ReadFixedBytes(SszBasicTypes.SyncCommitteeLength);
            var branchPost = reader.ReadFixedRootVector(LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork));

            if (headerOffset < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientBootstrap: header offset {headerOffset} precedes fixed section length {fixedLen} for fork {fork}.");
            }
            if (headerOffset > data.Length)
            {
                throw new InvalidOperationException(
                    $"LightClientBootstrap: header offset {headerOffset} exceeds buffer length {data.Length} for fork {fork}.");
            }

            var variableHeaderBytes = data.AsSpan((int)headerOffset).ToArray();

            LightClientHeader header;
            try
            {
                header = LightClientHeader.Decode(variableHeaderBytes, fork);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"LightClientBootstrap: inner header decode failed for fork {fork}: {ex.Message}", ex);
            }

            return new LightClientBootstrap
            {
                Fork = fork,
                Header = header,
                CurrentSyncCommittee = SyncCommittee.Decode(committeeBytesPost),
                CurrentSyncCommitteeBranch = branchPost
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
