using System.Collections.Generic;
using System.Linq;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class IsSyncCommitteeUpdatePredicateTests
    {
        [Fact]
        public void Given_NullBranch_When_CheckingPredicate_Then_ReturnsFalse()
        {
            var update = new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                NextSyncCommitteeBranch = null
            };

            Assert.False(InvokeIsSyncCommitteeUpdate(update));
        }

        [Fact]
        public void Given_EmptyBranch_When_CheckingPredicate_Then_ReturnsFalse()
        {
            var update = new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                NextSyncCommitteeBranch = new List<byte[]>()
            };

            Assert.False(InvokeIsSyncCommitteeUpdate(update));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        [InlineData(ConsensusFork.Capella)]
        [InlineData(ConsensusFork.Deneb)]
        [InlineData(ConsensusFork.Electra)]
        public void Given_DefaultZeroBranchAtForkLength_When_CheckingPredicate_Then_ReturnsFalse(ConsensusFork fork)
        {
            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(fork);
            var branch = new List<byte[]>(depth);
            for (var i = 0; i < depth; i++)
            {
                branch.Add(new byte[SszBasicTypes.RootLength]);
            }

            var update = new LightClientUpdate
            {
                Fork = fork,
                NextSyncCommitteeBranch = branch
            };

            Assert.False(InvokeIsSyncCommitteeUpdate(update));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Electra)]
        public void Given_NonZeroBranchAtForkLength_When_CheckingPredicate_Then_ReturnsTrue(ConsensusFork fork)
        {
            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(fork);
            var branch = new List<byte[]>(depth);
            for (var i = 0; i < depth; i++)
            {
                var root = new byte[SszBasicTypes.RootLength];
                root[0] = (byte)(i + 1);
                branch.Add(root);
            }

            var update = new LightClientUpdate
            {
                Fork = fork,
                NextSyncCommitteeBranch = branch
            };

            Assert.True(InvokeIsSyncCommitteeUpdate(update));
        }

        [Fact]
        public void Given_BranchWithSingleNonZeroByteInLastRoot_When_CheckingPredicate_Then_ReturnsTrue()
        {
            var depth = LightClientForkSpec.NextSyncCommitteeBranchDepth(ConsensusFork.Electra);
            var branch = new List<byte[]>(depth);
            for (var i = 0; i < depth - 1; i++)
            {
                branch.Add(new byte[SszBasicTypes.RootLength]);
            }

            var last = new byte[SszBasicTypes.RootLength];
            last[SszBasicTypes.RootLength - 1] = 0x01;
            branch.Add(last);

            var update = new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                NextSyncCommitteeBranch = branch
            };

            Assert.True(InvokeIsSyncCommitteeUpdate(update));
        }

        [Fact]
        public void Given_BranchWithMalformedRootLength_When_CheckingPredicate_Then_ReturnsFalse()
        {
            var branch = new List<byte[]>
            {
                new byte[SszBasicTypes.RootLength],
                new byte[16]
            };

            var update = new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                NextSyncCommitteeBranch = branch
            };

            Assert.False(InvokeIsSyncCommitteeUpdate(update));
        }

        private static bool InvokeIsSyncCommitteeUpdate(LightClientUpdate update)
        {
            return LightClientService.IsSyncCommitteeUpdate(update);
        }
    }
}
