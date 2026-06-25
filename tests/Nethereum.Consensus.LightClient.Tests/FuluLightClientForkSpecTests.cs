using Nethereum.Consensus.Ssz;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    /// <summary>
    /// Fulu LightClient fork-spec verification — confirms Fulu inherits the Electra
    /// LightClient gindex / branch-depth table verbatim because the Fulu spec defines no
    /// <c>light-client/</c> overrides (<see href="https://github.com/ethereum/consensus-specs/tree/master/specs/fulu">
    /// specs/fulu</see> contains no such subdirectory). Fulu's sole <c>BeaconState</c>
    /// change is appending <c>proposer_lookahead</c> (38th field) per
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/specs/fulu/beacon-chain.md">
    /// specs/fulu/beacon-chain.md</see> line 188; since 38 ≤ 64 = 2^6, the BeaconState
    /// merkle tree depth stays at 6 and all existing field gindices are preserved.
    /// FULU_FORK_VERSION (0x06000000) and FULU_FORK_EPOCH (411392 → mainnet activation
    /// slot 13,164,544 on 2025-12-03) are confirmed at
    /// <see href="https://raw.githubusercontent.com/ethereum/consensus-specs/master/configs/mainnet.yaml">
    /// configs/mainnet.yaml</see> lines 153–154.
    /// </summary>
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
            // 169 - 2^7 = 169 - 128 = 41
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
            // 86 - 2^6 = 86 - 64 = 22
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
            // 87 - 2^6 = 87 - 64 = 23
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
            // 25 - 2^4 = 25 - 16 = 9
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
            // FULU_FORK_EPOCH = 411392, slot = epoch * SLOTS_PER_EPOCH (32) = 13_164_544
            // configs/mainnet.yaml lines 153-154
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
