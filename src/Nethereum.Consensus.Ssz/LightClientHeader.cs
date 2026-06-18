using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// <c>LightClientHeader</c> SSZ container per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/altair/light-client/sync-protocol.md">
    /// specs/altair/light-client/sync-protocol.md</see> (Altair / Bellatrix: bare
    /// <c>BeaconBlockHeader</c>) and
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/capella/light-client/sync-protocol.md">
    /// specs/capella/light-client/sync-protocol.md</see> (Capella+: BeaconBlockHeader +
    /// ExecutionPayloadHeader + ExecutionBranch, depth 4 at <c>EXECUTION_PAYLOAD_GINDEX = 25</c>).
    /// </summary>
    public class LightClientHeader
    {
        public ConsensusFork Fork { get; set; } = ConsensusFork.Phase0;

        public BeaconBlockHeader Beacon { get; set; } = new BeaconBlockHeader();
        public ExecutionPayloadHeader Execution { get; set; } = new ExecutionPayloadHeader();
        public IList<byte[]> ExecutionBranch { get; set; } = new List<byte[]>();

        private static int ComputeFixedSectionLength(ConsensusFork fork)
        {
            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
                return SszBasicTypes.BeaconBlockHeaderLength;
            return SszBasicTypes.BeaconBlockHeaderLength +
                   sizeof(uint) +
                   SszBasicTypes.BranchByteLength(LightClientForkSpec.ExecutionBranchDepth(fork));
        }

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            var beaconBytes = Beacon.Encode();
            SszBasicTypes.ValidateFixedLength(beaconBytes, SszBasicTypes.BeaconBlockHeaderLength, nameof(Beacon));

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                return beaconBytes;
            }

            var executionBytes = Execution.Encode(fork);
            var executionBranchDepth = LightClientForkSpec.ExecutionBranchDepth(fork);
            var branch = NormalizeBranch(ExecutionBranch, nameof(ExecutionBranch), executionBranchDepth);
            var fixedLen = ComputeFixedSectionLength(fork);

            using var writer = new SszWriter();
            writer.WriteBytes(beaconBytes);
            writer.WriteUInt32((uint)fixedLen);
            writer.WriteFixedRootVector(branch, executionBranchDepth);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, executionBytes);
        }

        public static LightClientHeader Decode(byte[] data, ConsensusFork fork)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                SszBasicTypes.ValidateFixedLength(data, SszBasicTypes.BeaconBlockHeaderLength, nameof(data));
                return new LightClientHeader
                {
                    Fork = fork,
                    Beacon = BeaconBlockHeader.Decode(data)
                };
            }

            var fixedLen = ComputeFixedSectionLength(fork);
            if (data.Length < fixedLen)
            {
                throw new InvalidOperationException(
                    $"LightClientHeader buffer length {data.Length} below fixed section {fixedLen} for fork {fork}.");
            }

            var reader = new SszReader(data);
            var beaconBytes = reader.ReadFixedBytes(SszBasicTypes.BeaconBlockHeaderLength);
            var executionOffset = reader.ReadUInt32();
            var executionBranch = reader.ReadFixedRootVector(LightClientForkSpec.ExecutionBranchDepth(fork));

            ValidateOffset(data.Length, executionOffset, fixedLen);
            var executionSpan = data.AsSpan((int)executionOffset);

            ExecutionPayloadHeader execution;
            try
            {
                execution = ExecutionPayloadHeader.Decode(executionSpan, fork);
            }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to decode ExecutionPayloadHeader for fork {fork}.", ex);
            }

            return new LightClientHeader
            {
                Fork = fork,
                Beacon = BeaconBlockHeader.Decode(beaconBytes),
                ExecutionBranch = executionBranch,
                Execution = execution
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            AssertForkConsistency(fork);

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                return Beacon.HashTreeRoot();
            }

            var branchRoot = SszBasicTypes.HashTreeRootBranch(new List<byte[]>(ExecutionBranch));
            return SszMerkleizer.Merkleize(new[]
            {
                Beacon.HashTreeRoot(),
                Execution.HashTreeRoot(fork),
                branchRoot
            });
        }

        private static IList<byte[]> NormalizeBranch(IEnumerable<byte[]> branchEnumerable, string propertyName, int expectedCount)
        {
            if (branchEnumerable == null) throw new ArgumentNullException(propertyName);
            var branch = branchEnumerable as IList<byte[]> ?? new List<byte[]>(branchEnumerable);
            if (branch.Count != expectedCount)
            {
                throw new InvalidOperationException($"Execution branch must contain {expectedCount} roots.");
            }
            return branch;
        }

        private static void ValidateOffset(int totalLength, uint offset, int fixedLen)
        {
            if (offset < fixedLen || offset > totalLength)
            {
                throw new InvalidOperationException("Execution payload offset exceeds SSZ buffer length.");
            }
        }

        private void AssertForkConsistency(ConsensusFork fork)
        {
            if (Execution != null &&
                LightClientForkSpec.HasExecutionPayloadHeader(fork) &&
                Execution.Fork != fork)
            {
                throw new InvalidOperationException(
                    $"LightClientHeader.Execution.Fork={Execution.Fork} but outer fork is {fork}.");
            }
        }
    }
}
