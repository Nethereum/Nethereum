using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class FuluLightClientForkSpecTests
    {
        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void FinalizedRootGIndex_Fulu_ReturnsElectraValue_169()
        {
            Assert.Equal(LightClientForkSpec.FinalizedRootGIndexElectraPlus,
                LightClientForkSpec.FinalizedRootGIndex(ConsensusFork.Fulu));
            Assert.Equal(169, LightClientForkSpec.FinalizedRootGIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void FinalityBranchDepth_Fulu_Returns7()
        {
            Assert.Equal(7, LightClientForkSpec.FinalityBranchDepth(ConsensusFork.Fulu));
            Assert.Equal(7, LightClientForkSpec.FinalityBranchLength(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void FinalityBranchIndex_Fulu_Returns41()
        {
            Assert.Equal(41, LightClientForkSpec.FinalityBranchIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void CurrentSyncCommitteeGIndex_Fulu_ReturnsElectraValue_86()
        {
            Assert.Equal(86, LightClientForkSpec.CurrentSyncCommitteeGIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void CurrentSyncCommitteeBranchDepth_Fulu_Returns6()
        {
            Assert.Equal(6, LightClientForkSpec.CurrentSyncCommitteeBranchDepth(ConsensusFork.Fulu));
            Assert.Equal(6, LightClientForkSpec.CurrentSyncCommitteeBranchLength(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void CurrentSyncCommitteeBranchIndex_Fulu_Returns22()
        {
            Assert.Equal(22, LightClientForkSpec.CurrentSyncCommitteeBranchIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void NextSyncCommitteeGIndex_Fulu_ReturnsElectraValue_87()
        {
            Assert.Equal(87, LightClientForkSpec.NextSyncCommitteeGIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void NextSyncCommitteeBranchDepth_Fulu_Returns6()
        {
            Assert.Equal(6, LightClientForkSpec.NextSyncCommitteeBranchDepth(ConsensusFork.Fulu));
            Assert.Equal(6, LightClientForkSpec.NextSyncCommitteeBranchLength(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void NextSyncCommitteeBranchIndex_Fulu_Returns23()
        {
            Assert.Equal(23, LightClientForkSpec.NextSyncCommitteeBranchIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void ExecutionPayloadGIndex_Fulu_ReturnsCapellaValue_25()
        {
            Assert.Equal(25, LightClientForkSpec.ExecutionPayloadGIndex);
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void ExecutionBranchDepth_Fulu_Returns4()
        {
            Assert.Equal(4, LightClientForkSpec.ExecutionBranchDepth(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void ExecutionBranchIndex_Fulu_Returns9()
        {
            Assert.Equal(9, LightClientForkSpec.ExecutionBranchIndex(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void HasExecutionPayloadHeader_Fulu_ReturnsTrue()
        {
            Assert.True(LightClientForkSpec.HasExecutionPayloadHeader(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void HasExecutionPayloadContainer_Fulu_ReturnsTrue()
        {
            Assert.True(LightClientForkSpec.HasExecutionPayloadContainer(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void HasWithdrawalsRoot_Fulu_ReturnsTrue()
        {
            Assert.True(LightClientForkSpec.HasWithdrawalsRoot(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void HasBlobGasFields_Fulu_ReturnsTrue()
        {
            Assert.True(LightClientForkSpec.HasBlobGasFields(ConsensusFork.Fulu));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        public void ChainSpec_Mainnet_FuluActivation_Matches_Spec()
        {
            var spec = ChainSpec.Mainnet;
            var atActivation = spec.GetForkAtSlot(13_164_544UL);
            Assert.Equal(ConsensusFork.Fulu, atActivation);

            var oneBeforeActivation = spec.GetForkAtSlot(13_164_543UL);
            Assert.Equal(ConsensusFork.Electra, oneBeforeActivation);

            var forkVersion = spec.GetForkVersionAtSlot(13_164_544UL);
            Assert.Equal(new byte[] { 0x06, 0x00, 0x00, 0x00 }, forkVersion);
        }
    }
}
