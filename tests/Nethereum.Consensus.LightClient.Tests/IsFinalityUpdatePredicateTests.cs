using System.Collections.Generic;
using Nethereum.Consensus.LightClient;
using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class IsFinalityUpdatePredicateTests
    {
        [Fact]
        public void Given_NullBranch_When_CheckingPredicate_Then_ReturnsFalse()
        {
            var update = new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                FinalityBranch = null
            };

            Assert.False(InvokeIsFinalityUpdate(update));
        }

        [Fact]
        public void Given_EmptyBranch_When_CheckingPredicate_Then_ReturnsFalse()
        {
            var update = new LightClientUpdate
            {
                Fork = ConsensusFork.Electra,
                FinalityBranch = new List<byte[]>()
            };

            Assert.False(InvokeIsFinalityUpdate(update));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Bellatrix)]
        [InlineData(ConsensusFork.Capella)]
        [InlineData(ConsensusFork.Deneb)]
        [InlineData(ConsensusFork.Electra)]
        public void Given_DefaultZeroBranchAtForkLength_When_CheckingPredicate_Then_ReturnsFalse(ConsensusFork fork)
        {
            var depth = LightClientForkSpec.FinalityBranchDepth(fork);
            var branch = new List<byte[]>(depth);
            for (var i = 0; i < depth; i++)
            {
                branch.Add(new byte[SszBasicTypes.RootLength]);
            }

            var update = new LightClientUpdate
            {
                Fork = fork,
                FinalityBranch = branch
            };

            Assert.False(InvokeIsFinalityUpdate(update));
        }

        [Theory]
        [InlineData(ConsensusFork.Altair)]
        [InlineData(ConsensusFork.Electra)]
        public void Given_NonDefaultBranch_When_CheckingPredicate_Then_ReturnsTrue(ConsensusFork fork)
        {
            var depth = LightClientForkSpec.FinalityBranchDepth(fork);
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
                FinalityBranch = branch
            };

            Assert.True(InvokeIsFinalityUpdate(update));
        }

        [Fact]
        public void Given_SingleNonZeroByteInLastRoot_When_CheckingPredicate_Then_ReturnsTrue()
        {
            var depth = LightClientForkSpec.FinalityBranchDepth(ConsensusFork.Electra);
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
                FinalityBranch = branch
            };

            Assert.True(InvokeIsFinalityUpdate(update));
        }

        private static bool InvokeIsFinalityUpdate(LightClientUpdate update)
        {
            return LightClientService.IsFinalityUpdate(update);
        }
    }
}
