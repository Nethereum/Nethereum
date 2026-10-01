using System;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class SyncAggregateQuorumTests
    {
        [Fact]
        public void Given_NullAggregate_When_BaselineCheck_Then_ReturnsFalse()
        {
            Assert.False(InvokeHasBaselineParticipation(null));
        }

        [Fact]
        public void Given_ZeroParticipants_When_BaselineCheck_Then_ReturnsFalse()
        {
            var aggregate = CreateAggregate(participantCount: 0);

            Assert.False(InvokeHasBaselineParticipation(aggregate));
        }

        [Fact]
        public void Given_SingleParticipant_When_BaselineCheck_Then_ReturnsTrue()
        {
            var aggregate = CreateAggregate(participantCount: 1);

            Assert.True(InvokeHasBaselineParticipation(aggregate));
        }

        [Fact]
        public void Given_341Participants_When_SupermajorityCheck_Then_ReturnsFalse()
        {
            var aggregate = CreateAggregate(participantCount: 341);

            Assert.False(InvokeHasSupermajorityParticipation(aggregate));
        }

        [Fact]
        public void Given_342Participants_When_SupermajorityCheck_Then_ReturnsTrueAtExactMinimum()
        {
            var aggregate = CreateAggregate(participantCount: 342);

            Assert.True(InvokeHasSupermajorityParticipation(aggregate));
        }

        [Fact]
        public void Given_FullParticipation_When_SupermajorityCheck_Then_ReturnsTrue()
        {
            var aggregate = CreateAggregate(participantCount: SszBasicTypes.SyncCommitteeSize);

            Assert.True(InvokeHasSupermajorityParticipation(aggregate));
        }

        [Fact]
        public void Given_WrongLengthBits_When_BaselineCheck_Then_ThrowsInvalidOperation()
        {
            var aggregate = new SyncAggregate
            {
                SyncCommitteeBits = new byte[63],
                SyncCommitteeSignature = new byte[SszBasicTypes.SignatureLength]
            };

            Assert.Throws<InvalidOperationException>(() => InvokeHasBaselineParticipation(aggregate));
        }

        [Fact]
        public void Given_WrongLengthBits_When_SupermajorityCheck_Then_ThrowsInvalidOperation()
        {
            var aggregate = new SyncAggregate
            {
                SyncCommitteeBits = new byte[65],
                SyncCommitteeSignature = new byte[SszBasicTypes.SignatureLength]
            };

            Assert.Throws<InvalidOperationException>(() => InvokeHasSupermajorityParticipation(aggregate));
        }

        [Fact]
        public void Given_MinSyncCommitteeParticipantsConstant_Then_EqualsOne()
        {
            Assert.Equal(1, LightClientForkSpec.MinSyncCommitteeParticipants);
        }

        internal static SyncAggregate CreateAggregate(int participantCount)
        {
            if (participantCount < 0 || participantCount > SszBasicTypes.SyncCommitteeSize)
            {
                throw new ArgumentOutOfRangeException(nameof(participantCount));
            }

            var bits = new byte[SszBasicTypes.SyncCommitteeSize / 8];
            for (var i = 0; i < participantCount; i++)
            {
                bits[i / 8] |= (byte)(1 << (i % 8));
            }

            return new SyncAggregate
            {
                SyncCommitteeBits = bits,
                SyncCommitteeSignature = new byte[SszBasicTypes.SignatureLength]
            };
        }

        private static bool InvokeHasBaselineParticipation(SyncAggregate aggregate)
        {
            return LightClientService.HasBaselineParticipation(aggregate);
        }

        private static bool InvokeHasSupermajorityParticipation(SyncAggregate aggregate)
        {
            return LightClientService.HasSupermajorityParticipation(aggregate);
        }
    }
}
