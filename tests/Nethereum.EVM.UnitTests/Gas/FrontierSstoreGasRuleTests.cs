using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class FrontierSstoreGasRuleTests
    {
        [Fact]
        public void Given_ASlotFilledFromZero_When_Priced_Then_ChargesSstoreSet()
        {
            Assert.Equal(GasConstants.SSTORE_SET, Price(original: 0, current: 0, newValue: 9));
        }

        [Fact]
        public void Given_ASlotClearedToZero_When_Priced_Then_ChargesTheFlatReset()
        {
            Assert.Equal(GasConstants.SSTORE_RESET_PRE_BERLIN, Price(original: 9, current: 9, newValue: 0));
        }

        [Fact]
        public void Given_ASlotOverwrittenWithAnotherNonZeroValue_When_Priced_Then_ChargesTheFlatReset()
        {
            Assert.Equal(GasConstants.SSTORE_RESET_PRE_BERLIN, Price(original: 9, current: 9, newValue: 11));
        }

        [Fact]
        public void Given_AZeroSlotWrittenWithZero_When_Priced_Then_ChargesTheFlatReset()
        {
            Assert.Equal(GasConstants.SSTORE_RESET_PRE_BERLIN, Price(original: 0, current: 0, newValue: 0));
        }

        [Fact]
        public void Given_AColdSlot_When_Priced_Then_TheChargeIsUnchanged()
        {
            var cold = SstoreSlotState.Resolved(Word(0), Word(0), Word(9), isColdAccess: true);
            var warm = SstoreSlotState.Resolved(Word(0), Word(0), Word(9), isColdAccess: false);

            Assert.Equal(
                FrontierSstoreGasRule.Instance.GetGasCost(warm),
                FrontierSstoreGasRule.Instance.GetGasCost(cold));
        }

        [Fact]
        public void Given_TwoSlotsDifferingOnlyInTheirTransactionStartValue_When_Priced_Then_TheChargeIsTheSame()
        {
            Assert.Equal(
                Price(original: 0, current: 5, newValue: 9),
                Price(original: 3, current: 5, newValue: 9));
        }

        [Fact]
        public void Given_AnyGasRemaining_When_Asked_Then_TheRuleNeverRefuses()
        {
            Assert.False(FrontierSstoreGasRule.Instance.RefusesTheFrameBeforeReadingTheSlot(0));
            Assert.False(FrontierSstoreGasRule.Instance.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND));
        }

        private static long Price(byte original, byte current, byte newValue) =>
            FrontierSstoreGasRule.Instance.GetGasCost(
                SstoreSlotState.Resolved(Word(original), Word(current), Word(newValue), isColdAccess: false));

        private static byte[] Word(byte value)
        {
            var word = new byte[32];
            word[31] = value;
            return word;
        }
    }
}
