using System.Numerics;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class PoseidonParameterFactoryTests
    {
        [Theory]
        [InlineData(
            PoseidonParameterPreset.CircomT3,
            4,
            3,
            56,
            "11633431549750490989983886834189948010834808234699737327785600195936805266405",
            "16023668707004248971294664614290028914393192768609916554276071736843535714477")]
        [InlineData(
            PoseidonParameterPreset.CircomT6,
            7,
            6,
            63,
            "15193892625865514930501893609026366493846449603945567488151250645948827690215",
            "19332164824128329382868318451458022991369413618825711961282217322674570624669")]
        [InlineData(
            PoseidonParameterPreset.CircomT14,
            15,
            14,
            60,
            "9296474750911444465025945061626611450573261102544117494435956219223872045013",
            "1954546571818731885139861264947334230782822161673023234242993080695489129982")]
        [InlineData(
            PoseidonParameterPreset.CircomT16,
            17,
            16,
            68,
            "21579410516734741630578831791708254656585702717204712919233299001262271512412",
            "11497693837059016825308731789443585196852778517742143582474723527597064448312")]
        public void CircomPresetsMatchExpectedConstants(
            PoseidonParameterPreset preset,
            int expectedStateWidth,
            int expectedRate,
            int expectedPartialRounds,
            string expectedRoundConstant,
            string expectedMdsEntry)
        {
            var cached = PoseidonParameterFactory.GetPreset(preset);

            Assert.Equal(expectedStateWidth, cached.StateWidth);
            Assert.Equal(expectedRate, cached.Rate);
            Assert.Equal(expectedPartialRounds, cached.PartialRounds);
            Assert.Equal(BigInteger.Parse(expectedRoundConstant), cached.RoundConstants[0, 0]);
            Assert.Equal(BigInteger.Parse(expectedMdsEntry), cached.MdsMatrix[0, 0]);
        }

        [Fact]
        public void GetPresetCachesInstances()
        {
            var first = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT14);
            var second = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT14);

            Assert.Same(first, second);
        }

        [Fact]
        public void DefaultPresetIsCircomT3()
        {
            var fromDefault = PoseidonParameterFactory.GetPreset(PoseidonParameterFactory.DefaultPreset);
            var explicitPreset = PoseidonParameterFactory.GetPreset(PoseidonParameterPreset.CircomT3);

            Assert.Same(explicitPreset, fromDefault);
        }
    }
}
