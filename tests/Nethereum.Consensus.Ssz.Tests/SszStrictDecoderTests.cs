using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests
{
    /// <summary>
    /// Malformed-input tests for SSZ container decoders. Per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/ssz/simple-serialize.md">
    /// ssz/simple-serialize.md</see> Deserialization section, decoders must reject offsets
    /// that are out of order, out of range, mismatch the fixed section length, or leave
    /// trailing unused bytes.
    /// </summary>
    public class SszStrictDecoderTests
    {
        public static IEnumerable<object[]> AllForks()
        {
            yield return new object[] { ConsensusFork.Altair };
            yield return new object[] { ConsensusFork.Bellatrix };
            yield return new object[] { ConsensusFork.Capella };
            yield return new object[] { ConsensusFork.Deneb };
            yield return new object[] { ConsensusFork.Electra };
        }

        public static IEnumerable<object[]> ExecutionForks()
        {
            yield return new object[] { ConsensusFork.Capella };
            yield return new object[] { ConsensusFork.Deneb };
            yield return new object[] { ConsensusFork.Electra };
        }

        [Theory]
        [MemberData(nameof(AllForks))]
        public void Given_ShortBuffer_When_DecodingLightClientUpdate_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var data = new byte[8];

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientUpdate.Decode(data, fork));
            Assert.Contains("shorter than fixed section", ex.Message);
        }

        // Capella+ only: at Altair/Bellatrix LightClientUpdate has no header offsets
        // (LightClientHeader is fixed-size and inlined). Bytes 0..3 are the attested header's
        // Slot field, not an offset, so the corruption pattern below is meaningless pre-Capella.
        [Theory]
        [MemberData(nameof(ExecutionForks))]
        public void Given_AttestedOffsetBelowFixedSection_When_DecodingUpdate_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var update = SampleData.CreateUpdate();
            update.Fork = fork;
            update.AttestedHeader.Fork = fork;
            update.FinalizedHeader.Fork = fork;
            update.AttestedHeader.Execution.Fork = fork;
            update.FinalizedHeader.Execution.Fork = fork;
            ResizeBranchesForFork(update, fork);

            var encoded = update.Encode(fork);
            // Overwrite the attested-offset (first 4 bytes) with a value below fixed section.
            encoded[0] = 0;
            encoded[1] = 0;
            encoded[2] = 0;
            encoded[3] = 0;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientUpdate.Decode(encoded, fork));
            Assert.Contains("precedes fixed section length", ex.Message);
        }

        [Theory]
        [MemberData(nameof(ExecutionForks))]
        public void Given_AttestedOffsetBeyondBuffer_When_DecodingUpdate_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var update = SampleData.CreateUpdate();
            update.Fork = fork;
            update.AttestedHeader.Fork = fork;
            update.FinalizedHeader.Fork = fork;
            update.AttestedHeader.Execution.Fork = fork;
            update.FinalizedHeader.Execution.Fork = fork;
            ResizeBranchesForFork(update, fork);

            var encoded = update.Encode(fork);
            // Overwrite the attested-offset with a value beyond the buffer.
            var beyond = (uint)(encoded.Length + 100);
            BitConverter.GetBytes(beyond).CopyTo(encoded, 0);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientUpdate.Decode(encoded, fork));
            Assert.Contains("exceeds SSZ buffer length", ex.Message);
        }

        [Fact]
        public void Given_FinalizedOffsetPrecedesAttestedOffset_When_DecodingUpdate_Then_Throws()
        {
            var fork = ConsensusFork.Electra;
            var update = SampleData.CreateUpdate();
            var encoded = update.Encode(fork);

            var fixedLen = ComputeFixedSectionLengthForUpdate(fork);
            var attestedOffset = BitConverter.ToUInt32(encoded, 0);
            // finalized_offset position: 4 + SyncCommitteeLength + NextBranchByteLength
            var nextBranchByteLen = LightClientForkSpec.NextSyncCommitteeBranchLength(fork) * SszBasicTypes.RootLength;
            var finalizedOffsetPos = sizeof(uint) + SszBasicTypes.SyncCommitteeLength + nextBranchByteLen;

            // Force finalizedOffset = attestedOffset - 1 (still > fixedLen if attestedOffset==fixedLen+1, but
            // we set attestedOffset to the maximum within bounds and finalized below it).
            BitConverter.GetBytes((uint)fixedLen + 1u).CopyTo(encoded, 0);
            BitConverter.GetBytes((uint)fixedLen).CopyTo(encoded, finalizedOffsetPos);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientUpdate.Decode(encoded, fork));
            Assert.Contains("precedes", ex.Message);
        }

        [Theory]
        [MemberData(nameof(AllForks))]
        public void Given_ShortBuffer_When_DecodingLightClientFinalityUpdate_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var data = new byte[8];

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientFinalityUpdate.Decode(data, fork));
            Assert.Contains("shorter than fixed section", ex.Message);
        }

        [Theory]
        [MemberData(nameof(AllForks))]
        public void Given_NullData_When_DecodingLightClientFinalityUpdate_Then_ThrowsArgumentNull(ConsensusFork fork)
        {
            Assert.Throws<ArgumentNullException>(() =>
                LightClientFinalityUpdate.Decode(null, fork));
        }

        [Theory]
        [MemberData(nameof(AllForks))]
        public void Given_ShortBuffer_When_DecodingLightClientBootstrap_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var data = new byte[8];

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientBootstrap.Decode(data, fork));
            Assert.Contains("below fixed section", ex.Message);
        }

        // Capella+ only: at Altair/Bellatrix LightClientBootstrap has no header offset
        // (LightClientHeader is fixed-size and inlined). Bytes 0..3 are the inlined header's
        // Slot field, not an offset, so the corruption pattern below is meaningless pre-Capella.
        [Theory]
        [MemberData(nameof(ExecutionForks))]
        public void Given_HeaderOffsetBelowFixedSection_When_DecodingBootstrap_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var bootstrap = SampleData.CreateBootstrap();
            bootstrap.Fork = fork;
            bootstrap.Header.Fork = fork;
            bootstrap.Header.Execution.Fork = fork;
            bootstrap.CurrentSyncCommitteeBranch = ResizeBranch(bootstrap.CurrentSyncCommitteeBranch,
                LightClientForkSpec.CurrentSyncCommitteeBranchLength(fork));

            var encoded = bootstrap.Encode(fork);
            encoded[0] = 0;
            encoded[1] = 0;
            encoded[2] = 0;
            encoded[3] = 0;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientBootstrap.Decode(encoded, fork));
            Assert.Contains("precedes fixed section length", ex.Message);
        }

        [Theory]
        [MemberData(nameof(AllForks))]
        public void Given_ShortBuffer_When_DecodingLightClientOptimisticUpdate_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var data = new byte[8];

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientOptimisticUpdate.Decode(data, fork));
            Assert.Contains("below fixed section", ex.Message);
        }

        [Theory]
        [MemberData(nameof(ExecutionForks))]
        public void Given_ShortBuffer_When_DecodingLightClientHeader_Then_ThrowsInvalidOperation(ConsensusFork fork)
        {
            var data = new byte[8];

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientHeader.Decode(data, fork));
            Assert.Contains("below fixed section", ex.Message);
        }

        [Fact]
        public void Given_ExecutionOffsetBelowFixedSection_When_DecodingHeader_Then_DistinctErrorMessage()
        {
            var fork = ConsensusFork.Electra;
            var header = SampleData.CreateLightClientHeader();
            var encoded = header.Encode(fork);

            // Overwrite the execution-offset (located at the 112-byte BeaconBlockHeader offset).
            BitConverter.GetBytes(0u).CopyTo(encoded, SszBasicTypes.BeaconBlockHeaderLength);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientHeader.Decode(encoded, fork));
            Assert.Contains("precedes fixed section length", ex.Message);
        }

        [Fact]
        public void Given_ExecutionOffsetBeyondBuffer_When_DecodingHeader_Then_DistinctErrorMessage()
        {
            var fork = ConsensusFork.Electra;
            var header = SampleData.CreateLightClientHeader();
            var encoded = header.Encode(fork);

            var beyond = (uint)(encoded.Length + 1000);
            BitConverter.GetBytes(beyond).CopyTo(encoded, SszBasicTypes.BeaconBlockHeaderLength);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                LightClientHeader.Decode(encoded, fork));
            Assert.Contains("exceeds SSZ buffer length", ex.Message);
        }

        private static int ComputeFixedSectionLengthForUpdate(ConsensusFork fork) =>
            sizeof(uint) +
            SszBasicTypes.SyncCommitteeLength +
            (LightClientForkSpec.NextSyncCommitteeBranchLength(fork) * SszBasicTypes.RootLength) +
            sizeof(uint) +
            (LightClientForkSpec.FinalityBranchLength(fork) * SszBasicTypes.RootLength) +
            SszBasicTypes.SyncAggregateLength +
            sizeof(ulong);

        private static void ResizeBranchesForFork(LightClientUpdate update, ConsensusFork fork)
        {
            update.NextSyncCommitteeBranch = ResizeBranch(update.NextSyncCommitteeBranch,
                LightClientForkSpec.NextSyncCommitteeBranchLength(fork));
            update.FinalityBranch = ResizeBranch(update.FinalityBranch,
                LightClientForkSpec.FinalityBranchLength(fork));

            if (LightClientForkSpec.HasExecutionPayloadHeader(fork))
            {
                update.AttestedHeader.ExecutionBranch = ResizeBranch(update.AttestedHeader.ExecutionBranch,
                    LightClientForkSpec.ExecutionBranchDepth(fork));
                update.FinalizedHeader.ExecutionBranch = ResizeBranch(update.FinalizedHeader.ExecutionBranch,
                    LightClientForkSpec.ExecutionBranchDepth(fork));
            }
        }

        private static IList<byte[]> ResizeBranch(IList<byte[]> branch, int targetLength)
        {
            var list = new List<byte[]>(targetLength);
            for (var i = 0; i < targetLength; i++)
            {
                if (i < branch.Count) list.Add(branch[i]);
                else list.Add(new byte[SszBasicTypes.RootLength]);
            }
            return list;
        }
    }
}
