using System;
using Xunit;

namespace Nethereum.Consensus.Ssz.Tests
{
    public class ChainSpecActivationTableTests
    {
        [Theory]
        [Trait("Category", "ConsensusSpec")]
        [Trait("Source", "configs/mainnet.yaml")]
        [InlineData(0UL, ConsensusFork.Phase0)]
        [InlineData(2_375_679UL, ConsensusFork.Phase0)]
        [InlineData(2_375_680UL, ConsensusFork.Altair)]
        [InlineData(4_636_671UL, ConsensusFork.Altair)]
        [InlineData(4_636_672UL, ConsensusFork.Bellatrix)]
        [InlineData(6_209_535UL, ConsensusFork.Bellatrix)]
        [InlineData(6_209_536UL, ConsensusFork.Capella)]
        [InlineData(8_626_175UL, ConsensusFork.Capella)]
        [InlineData(8_626_176UL, ConsensusFork.Deneb)]
        [InlineData(11_649_023UL, ConsensusFork.Deneb)]
        [InlineData(11_649_024UL, ConsensusFork.Electra)]
        [InlineData(13_164_543UL, ConsensusFork.Electra)]
        [InlineData(13_164_544UL, ConsensusFork.Fulu)]
        public void Given_MainnetSlot_When_GetForkAtSlot_Then_ReturnsExpectedFork(ulong slot, ConsensusFork expected)
        {
            Assert.Equal(expected, ChainSpec.Mainnet.GetForkAtSlot(slot));
        }

        [Theory]
        [Trait("Category", "ConsensusSpec")]
        [Trait("Source", "configs/mainnet.yaml")]
        [InlineData(0UL, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(2_375_679UL, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
        [InlineData(2_375_680UL, new byte[] { 0x01, 0x00, 0x00, 0x00 })]
        [InlineData(4_636_672UL, new byte[] { 0x02, 0x00, 0x00, 0x00 })]
        [InlineData(6_209_536UL, new byte[] { 0x03, 0x00, 0x00, 0x00 })]
        [InlineData(8_626_176UL, new byte[] { 0x04, 0x00, 0x00, 0x00 })]
        [InlineData(11_649_024UL, new byte[] { 0x05, 0x00, 0x00, 0x00 })]
        [InlineData(13_164_544UL, new byte[] { 0x06, 0x00, 0x00, 0x00 })]
        public void Given_MainnetSlot_When_GetForkVersionAtSlot_Then_ReturnsExpectedForkVersion(ulong slot, byte[] expected)
        {
            Assert.Equal(expected, ChainSpec.Mainnet.GetForkVersionAtSlot(slot));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        [Trait("Source", "configs/mainnet.yaml#L60")]
        public void Given_GloasTerritorySlot_When_GetForkAtSlot_Then_Throws()
        {
            Assert.Throws<NotSupportedException>(() => ChainSpec.Mainnet.GetForkAtSlot(ulong.MaxValue));
        }

        [Fact]
        [Trait("Category", "ConsensusSpec")]
        [Trait("Source", "configs/mainnet.yaml#L60")]
        public void Given_GloasTerritorySlot_When_GetForkVersionAtSlot_Then_Throws()
        {
            Assert.Throws<NotSupportedException>(() => ChainSpec.Mainnet.GetForkVersionAtSlot(ulong.MaxValue));
        }

        [Fact]
        public void Given_ReturnedForkVersion_When_Mutated_Then_DoesNotAffectTable()
        {
            var v1 = ChainSpec.Mainnet.GetForkVersionAtSlot(0UL);
            v1[0] = 0xFF;
            var v2 = ChainSpec.Mainnet.GetForkVersionAtSlot(0UL);
            Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, v2);
        }

        [Fact]
        public void Given_ForkVersionNotFourBytes_When_ConstructingForkActivation_Then_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new ChainSpec.ForkActivation(0UL, ConsensusFork.Phase0, new byte[3]));
        }

        [Fact]
        public void Given_NullForkVersion_When_ConstructingForkActivation_Then_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ChainSpec.ForkActivation(0UL, ConsensusFork.Phase0, null!));
        }

        [Fact]
        public void Mainnet_SlotsPerEpoch_IsThirtyTwo()
        {
            Assert.Equal(32UL, ChainSpec.Mainnet.SlotsPerEpoch);
        }
    }
}
