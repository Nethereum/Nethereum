using System;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests
{
    /// <summary>
    /// Strict length tests for <see cref="SyncAggregate.Decode(System.ReadOnlySpan{byte})"/>.
    /// Per <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/ssz/simple-serialize.md">
    /// ssz/simple-serialize.md</see>, a Bitvector[512] serialises to exactly 64 bytes; the
    /// SyncAggregate container totals 160 bytes (64 + 96 signature).
    /// </summary>
    public class SyncAggregateStrictDecodeTests
    {
        [Fact]
        public void Given_ExactLengthBuffer_When_Decoding_Then_Succeeds()
        {
            var data = new byte[SszBasicTypes.SyncAggregateLength];

            var result = SyncAggregate.Decode(data);

            Assert.NotNull(result);
            Assert.Equal(SszBasicTypes.SyncCommitteeSize / 8, result.SyncCommitteeBits.Length);
            Assert.Equal(SszBasicTypes.SignatureLength, result.SyncCommitteeSignature.Length);
        }

        [Fact]
        public void Given_TooShortBuffer_When_Decoding_Then_ThrowsInvalidOperation()
        {
            var data = new byte[SszBasicTypes.SyncAggregateLength - 1];

            var ex = Assert.Throws<InvalidOperationException>(() => SyncAggregate.Decode(data));
            Assert.Contains("must equal", ex.Message);
        }

        [Fact]
        public void Given_TooLongBuffer_When_Decoding_Then_ThrowsInvalidOperation()
        {
            var data = new byte[SszBasicTypes.SyncAggregateLength + 1];

            var ex = Assert.Throws<InvalidOperationException>(() => SyncAggregate.Decode(data));
            Assert.Contains("must equal", ex.Message);
        }

        [Fact]
        public void Given_EmptyBuffer_When_Decoding_Then_ThrowsInvalidOperation()
        {
            var data = Array.Empty<byte>();

            var ex = Assert.Throws<InvalidOperationException>(() => SyncAggregate.Decode(data));
            Assert.Contains("must equal", ex.Message);
        }
    }
}
