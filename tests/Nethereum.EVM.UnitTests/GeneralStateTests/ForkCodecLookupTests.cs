using Nethereum.EVM;
using Nethereum.Model.Codecs;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Each fork's lookup MUST return the same codec instance that the
    /// fork's spec registers — otherwise <c>config.HeaderCodec</c> at one
    /// call site and <c>BlockHeaderCodecs.ForFork(fork)</c> at another
    /// silently disagree.
    /// </summary>
    public class ForkCodecLookupTests
    {
        [Theory]
        [InlineData(HardforkName.Frontier)]
        [InlineData(HardforkName.Homestead)]
        [InlineData(HardforkName.TangerineWhistle)]
        [InlineData(HardforkName.SpuriousDragon)]
        [InlineData(HardforkName.Byzantium)]
        [InlineData(HardforkName.Constantinople)]
        [InlineData(HardforkName.Petersburg)]
        [InlineData(HardforkName.Istanbul)]
        [InlineData(HardforkName.Berlin)]
        [InlineData(HardforkName.London)]
        [InlineData(HardforkName.Paris)]
        [InlineData(HardforkName.Shanghai)]
        [InlineData(HardforkName.Cancun)]
        [InlineData(HardforkName.Prague)]
        [InlineData(HardforkName.Osaka)]
        public void HeaderCodec_LookupMatchesSpecRegistration(HardforkName fork)
        {
            var lookup = BlockHeaderCodecs.ForFork(fork);
            var fromSpec = ResolveSpec(fork).HeaderCodec;

            Assert.Same(fromSpec, lookup);
        }

        [Theory]
        [InlineData(HardforkName.Frontier)]
        [InlineData(HardforkName.Homestead)]
        [InlineData(HardforkName.TangerineWhistle)]
        [InlineData(HardforkName.SpuriousDragon)]
        [InlineData(HardforkName.Byzantium)]
        [InlineData(HardforkName.Constantinople)]
        [InlineData(HardforkName.Petersburg)]
        [InlineData(HardforkName.Istanbul)]
        [InlineData(HardforkName.Berlin)]
        [InlineData(HardforkName.London)]
        [InlineData(HardforkName.Paris)]
        [InlineData(HardforkName.Shanghai)]
        [InlineData(HardforkName.Cancun)]
        [InlineData(HardforkName.Prague)]
        [InlineData(HardforkName.Osaka)]
        public void ReceiptCodec_LookupMatchesSpecRegistration(HardforkName fork)
        {
            var lookup = ReceiptCodecs.ForFork(fork);
            var fromSpec = ResolveSpec(fork).ReceiptCodec;

            Assert.Same(fromSpec, lookup);
        }

        [Theory]
        [InlineData(HardforkName.Frontier)]
        [InlineData(HardforkName.Homestead)]
        [InlineData(HardforkName.TangerineWhistle)]
        [InlineData(HardforkName.SpuriousDragon)]
        [InlineData(HardforkName.Byzantium)]
        [InlineData(HardforkName.Constantinople)]
        [InlineData(HardforkName.Petersburg)]
        [InlineData(HardforkName.Istanbul)]
        [InlineData(HardforkName.Berlin)]
        [InlineData(HardforkName.London)]
        [InlineData(HardforkName.Paris)]
        [InlineData(HardforkName.Shanghai)]
        [InlineData(HardforkName.Cancun)]
        [InlineData(HardforkName.Prague)]
        [InlineData(HardforkName.Osaka)]
        public void TransactionDecoder_LookupMatchesSpecRegistration(HardforkName fork)
        {
            var lookup = TransactionDecoders.ForFork(fork);
            var fromSpec = ResolveSpec(fork).TransactionDecoder;

            Assert.Same(fromSpec, lookup);
        }

        private static Nethereum.EVM.Hardforks.HardforkSpec ResolveSpec(HardforkName fork)
        {
            switch (fork)
            {
                case HardforkName.Frontier: return Nethereum.EVM.Hardforks.FrontierSpec.Instance;
                case HardforkName.Homestead: return Nethereum.EVM.Hardforks.HomesteadSpec.Instance;
                case HardforkName.TangerineWhistle: return Nethereum.EVM.Hardforks.TangerineWhistleSpec.Instance;
                case HardforkName.SpuriousDragon: return Nethereum.EVM.Hardforks.SpuriousDragonSpec.Instance;
                case HardforkName.Byzantium: return Nethereum.EVM.Hardforks.ByzantiumSpec.Instance;
                case HardforkName.Constantinople: return Nethereum.EVM.Hardforks.ConstantinopleSpec.Instance;
                case HardforkName.Petersburg: return Nethereum.EVM.Hardforks.PetersburgSpec.Instance;
                case HardforkName.Istanbul: return Nethereum.EVM.Hardforks.IstanbulSpec.Instance;
                case HardforkName.Berlin: return Nethereum.EVM.Hardforks.BerlinSpec.Instance;
                case HardforkName.London: return Nethereum.EVM.Hardforks.LondonSpec.Instance;
                case HardforkName.Paris: return Nethereum.EVM.Hardforks.ParisSpec.Instance;
                case HardforkName.Shanghai: return Nethereum.EVM.Hardforks.ShanghaiSpec.Instance;
                case HardforkName.Cancun: return Nethereum.EVM.Hardforks.CancunSpec.Instance;
                case HardforkName.Prague: return Nethereum.EVM.Hardforks.PragueSpec.Instance;
                case HardforkName.Osaka: return Nethereum.EVM.Hardforks.OsakaSpec.Instance;
                default: throw new System.ArgumentOutOfRangeException(nameof(fork));
            }
        }
    }
}
