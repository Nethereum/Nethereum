using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Intrinsic;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class AmsterdamBlobScheduleTests
    {
        private const int AmsterdamUpdateFraction = 11_684_671;

        private static readonly EvmUInt256 ExcessBlobGas = new EvmUInt256(50_000_000);

        private static EvmUInt256 FeeUnder(IBlobGasRule rule) => rule.CalculateBlobBaseFee(ExcessBlobGas);

        [Fact]
        public void Given_TheAmsterdamRuleSet_When_ItsBlobRuleIsRead_Then_ItIsAmsterdamsOwnRuleNotMainnetsBpo2()
        {
            var rule = IntrinsicGasRuleSets.Amsterdam.Blob;

            Assert.IsType<AmsterdamBlobGasRule>(rule);
            Assert.NotSame(Eip7892Bpo2BlobGasRule.Instance, rule);
        }

        [Fact]
        public void Given_AmsterdamAndMainnetBpo2_When_TheSameExcessIsPriced_Then_TheyAgreeToday_ByCoincidenceNotBySharing()
        {
            Assert.Equal(FeeUnder(Eip7892Bpo2BlobGasRule.Instance), FeeUnder(AmsterdamBlobGasRule.Instance));
            Assert.NotSame(Eip7892Bpo2BlobGasRule.Instance, (IBlobGasRule)AmsterdamBlobGasRule.Instance);
        }

        [Fact]
        public void Given_TheAmsterdamRule_When_BuiltExplicitlyFromTheDocumentedFraction_Then_ItMatchesTheDefault()
        {
            var explicitly = new AmsterdamBlobGasRule(AmsterdamUpdateFraction);

            Assert.Equal(FeeUnder(AmsterdamBlobGasRule.Instance), FeeUnder(explicitly));
            Assert.Equal(AmsterdamUpdateFraction, AmsterdamBlobGasRule.DEFAULT_BASE_FEE_UPDATE_FRACTION);
        }

        [Fact]
        public void Given_EveryBlobScheduleInTheChain_When_TheSameExcessIsPriced_Then_EachRaiseOfTheFractionLowersTheFee()
        {
            var cancun = FeeUnder(Eip4844BlobGasRule.Instance);
            var prague = FeeUnder(Eip7691BlobGasRule.Instance);
            var osaka = FeeUnder(Eip7892BlobGasRule.Instance);
            var bpo1 = FeeUnder(Eip7892Bpo1BlobGasRule.Instance);
            var bpo2 = FeeUnder(Eip7892Bpo2BlobGasRule.Instance);
            var amsterdam = FeeUnder(AmsterdamBlobGasRule.Instance);

            Assert.True(prague < cancun, $"prague {prague} should be below cancun {cancun}");
            Assert.Equal(prague, osaka);
            Assert.True(bpo1 < osaka, $"bpo1 {bpo1} should be below osaka {osaka}");
            Assert.True(bpo2 < bpo1, $"bpo2 {bpo2} should be below bpo1 {bpo1}");
            Assert.Equal(bpo2, amsterdam);
        }

        [Fact]
        public void Given_TwoSchedulesWithDifferentFractions_When_TheSameExcessIsPriced_Then_TheFeesActuallyDiffer()
        {
            Assert.NotEqual(FeeUnder(Eip4844BlobGasRule.Instance), FeeUnder(AmsterdamBlobGasRule.Instance));
        }
    }
}
