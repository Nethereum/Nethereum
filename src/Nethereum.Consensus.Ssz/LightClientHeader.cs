using System;
using System.Collections.Generic;
using Nethereum.Ssz;

namespace Nethereum.Consensus.Ssz
{
    public class LightClientHeader
    {
        private static readonly int FixedSectionLength =
            SszBasicTypes.BeaconBlockHeaderLength +
            sizeof(uint) +
            SszBasicTypes.BranchByteLength(SszBasicTypes.ExecutionBranchLength);

        public BeaconBlockHeader Beacon { get; set; } = new BeaconBlockHeader();
        public ExecutionPayloadHeader Execution { get; set; } = new ExecutionPayloadHeader();
        public IList<byte[]> ExecutionBranch { get; set; } = new List<byte[]>();

        public byte[] Encode()
        {
            var beaconBytes = Beacon.Encode();
            SszBasicTypes.ValidateFixedLength(beaconBytes, SszBasicTypes.BeaconBlockHeaderLength, nameof(Beacon));

            var executionBytes = Execution.Encode();
            var branch = NormalizeBranch(ExecutionBranch, nameof(ExecutionBranch));

            using var writer = new SszWriter();
            writer.WriteBytes(beaconBytes);
            writer.WriteUInt32((uint)FixedSectionLength);
            writer.WriteFixedRootVector(branch, SszBasicTypes.ExecutionBranchLength);

            var fixedSection = writer.ToArray();
            return SszContainerEncoding.Combine(fixedSection, executionBytes);
        }

        public static LightClientHeader Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var reader = new SszReader(data);
            var beaconBytes = reader.ReadFixedBytes(SszBasicTypes.BeaconBlockHeaderLength);
            var executionOffset = reader.ReadUInt32();
            var executionBranch = reader.ReadFixedRootVector(SszBasicTypes.ExecutionBranchLength);

            ValidateOffset(data.Length, executionOffset);
            var executionSpan = data.AsSpan((int)executionOffset);

            return new LightClientHeader
            {
                Beacon = BeaconBlockHeader.Decode(beaconBytes),
                ExecutionBranch = executionBranch,
                Execution = ExecutionPayloadHeader.Decode(executionSpan)
            };
        }

        public byte[] HashTreeRoot()
        {
            var branchRoot = SszBasicTypes.HashTreeRootBranch(new List<byte[]>(ExecutionBranch));
            return SszMerkleizer.Merkleize(new[]
            {
                Beacon.HashTreeRoot(),
                Execution.HashTreeRoot(),
                branchRoot
            });
        }

        private static IList<byte[]> NormalizeBranch(IEnumerable<byte[]> branchEnumerable, string propertyName)
        {
            if (branchEnumerable == null) throw new ArgumentNullException(propertyName);
            var branch = branchEnumerable as IList<byte[]> ?? new List<byte[]>(branchEnumerable);
            if (branch.Count != SszBasicTypes.ExecutionBranchLength)
            {
                throw new InvalidOperationException($"Execution branch must contain {SszBasicTypes.ExecutionBranchLength} roots.");
            }

            return branch;
        }

        private static void ValidateOffset(int totalLength, uint offset)
        {
            if (offset < FixedSectionLength || offset > totalLength)
            {
                throw new InvalidOperationException("Execution payload offset exceeds SSZ buffer length.");
            }
        }
    }
}
