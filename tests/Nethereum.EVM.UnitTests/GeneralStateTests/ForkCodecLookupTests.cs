using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Nethereum.EVM.Hardforks;
using Nethereum.Model.Codecs;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class ForkCodecLookupTests
    {
        public static IEnumerable<object[]> RegisteredForkNames =>
            HardforkSpecRegistry.All.Select(spec => new object[] { spec.Name });

        [Theory]
        [MemberData(nameof(RegisteredForkNames))]
        public void HeaderCodec_LookupMatchesSpecRegistration(HardforkName fork)
        {
            var lookup = BlockHeaderCodecs.ForFork(fork);
            var fromSpec = ResolveSpec(fork).HeaderCodec;

            Assert.Same(fromSpec, lookup);
        }

        [Theory]
        [InlineData(HardforkName.FrontierThawing)]
        [InlineData(HardforkName.DaoFork)]
        [InlineData(HardforkName.MuirGlacier)]
        [InlineData(HardforkName.ArrowGlacier)]
        [InlineData(HardforkName.GrayGlacier)]
        public void HeaderCodec_BombDelayForks_ResolveToNeighbourCodec(HardforkName fork)
        {
            var codec = BlockHeaderCodecs.ForFork(fork);
            var expected = fork == HardforkName.ArrowGlacier || fork == HardforkName.GrayGlacier
                ? (IBlockHeaderCodec)LondonBlockHeaderCodec.Instance
                : LegacyBlockHeaderCodec.Instance;
            Assert.Same(expected, codec);
        }

        [Fact]
        public void HeaderCodec_EveryHardforkName_HasACodec()
        {
            foreach (HardforkName fork in System.Enum.GetValues(typeof(HardforkName)))
            {
                if (fork == HardforkName.Unspecified) continue;
                Assert.NotNull(BlockHeaderCodecs.ForFork(fork));
            }
        }

        [Fact]
        public void ReceiptCodec_EveryHardforkName_HasACodec()
        {
            foreach (HardforkName fork in System.Enum.GetValues(typeof(HardforkName)))
            {
                if (fork == HardforkName.Unspecified) continue;
                Assert.NotNull(ReceiptCodecs.ForFork(fork));
            }
        }

        [Fact]
        public void TransactionDecoder_EveryHardforkName_HasADecoder()
        {
            foreach (HardforkName fork in System.Enum.GetValues(typeof(HardforkName)))
            {
                if (fork == HardforkName.Unspecified) continue;
                Assert.NotNull(TransactionDecoders.ForFork(fork));
            }
        }

        [Theory]
        [MemberData(nameof(RegisteredForkNames))]
        public void ReceiptCodec_LookupMatchesSpecRegistration(HardforkName fork)
        {
            var lookup = ReceiptCodecs.ForFork(fork);
            var fromSpec = ResolveSpec(fork).ReceiptCodec;

            Assert.Same(fromSpec, lookup);
        }

        [Theory]
        [MemberData(nameof(RegisteredForkNames))]
        public void TransactionDecoder_LookupMatchesSpecRegistration(HardforkName fork)
        {
            var lookup = TransactionDecoders.ForFork(fork);
            var fromSpec = ResolveSpec(fork).TransactionDecoder;

            Assert.Same(fromSpec, lookup);
        }

        private static HardforkSpec ResolveSpec(HardforkName fork)
            => HardforkSpecRegistry.All.Single(spec => spec.Name == fork);
    }
}
