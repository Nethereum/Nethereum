using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// LightClientHeader as defined in the consensus spec.
    /// Altair / Bellatrix wire-shape: a bare <see cref="BeaconBlockHeader"/>.
    /// Capella+: BeaconBlockHeader + ExecutionPayloadHeader + ExecutionBranch (4 roots).
    /// </summary>
    public class LightClientHeader
    {
        public ConsensusFork Fork { get; set; } = ConsensusFork.Electra;

        public BeaconBlockHeader Beacon { get; set; } = new BeaconBlockHeader();
        public ExecutionPayloadHeader Execution { get; set; } = new ExecutionPayloadHeader();
        public IList<byte[]> ExecutionBranch { get; set; } = new List<byte[]>();

        // Capella+ container has a variable section (the execution payload header).
        // Pre-Capella: just BeaconBlockHeader.
        private static int ComputeFixedSectionLength(ConsensusFork fork)
        {
            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
                return SszBasicTypes.BeaconBlockHeaderLength;
            return SszBasicTypes.BeaconBlockHeaderLength +
                   sizeof(uint) +
                   SszBasicTypes.BranchByteLength(LightClientForkSpec.ExecutionBranchDepth);
        }

        public byte[] Encode() => Encode(Fork);

        public byte[] Encode(ConsensusFork fork)
        {
            var beaconBytes = Beacon.Encode();
            SszBasicTypes.ValidateFixedLength(beaconBytes, SszBasicTypes.BeaconBlockHeaderLength, nameof(Beacon));

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                // Altair/Bellatrix: LightClientHeader is just BeaconBlockHeader.
                return beaconBytes;
            }

            var executionBytes = Execution.Encode(fork);
            var branch = NormalizeBranch(ExecutionBranch, nameof(ExecutionBranch), LightClientForkSpec.ExecutionBranchDepth);
            var fixedLen = ComputeFixedSectionLength(fork);

            using var writer = new SszWriter();
            writer.WriteBytes(beaconBytes);
            writer.WriteUInt32((uint)fixedLen);
            writer.WriteFixedRootVector(branch, LightClientForkSpec.ExecutionBranchDepth);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, executionBytes);
        }

        public static LightClientHeader Decode(byte[] data, ConsensusFork fork)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                // Altair/Bellatrix: payload IS the BeaconBlockHeader; no offsets, no branch.
                SszBasicTypes.ValidateFixedLength(data, SszBasicTypes.BeaconBlockHeaderLength, nameof(data));
                return new LightClientHeader
                {
                    Fork = fork,
                    Beacon = BeaconBlockHeader.Decode(data)
                };
            }

            var reader = new SszReader(data);
            var beaconBytes = reader.ReadFixedBytes(SszBasicTypes.BeaconBlockHeaderLength);
            var executionOffset = reader.ReadUInt32();
            var executionBranch = reader.ReadFixedRootVector(LightClientForkSpec.ExecutionBranchDepth);

            ValidateOffset(data.Length, executionOffset, ComputeFixedSectionLength(fork));
            var executionSpan = data.AsSpan((int)executionOffset);

            return new LightClientHeader
            {
                Fork = fork,
                Beacon = BeaconBlockHeader.Decode(beaconBytes),
                ExecutionBranch = executionBranch,
                Execution = ExecutionPayloadHeader.Decode(executionSpan, fork)
            };
        }

        public byte[] HashTreeRoot() => HashTreeRoot(Fork);

        public byte[] HashTreeRoot(ConsensusFork fork)
        {
            if (!LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                // Altair/Bellatrix: htr is the BeaconBlockHeader's htr.
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
    }
}
