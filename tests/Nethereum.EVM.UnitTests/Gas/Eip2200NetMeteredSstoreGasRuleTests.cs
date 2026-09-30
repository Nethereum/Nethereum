using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip2200NetMeteredSstoreGasRuleTests
    {
        private const long ConstantinopleSload = 200;
        private const long IstanbulSload = 800;

        private static readonly ISstoreGasRule Eip1283 =
            Eip2200NetMeteredSstoreGasRule.AsIntroducedByEip1283(ConstantinopleSload);

        private static readonly ISstoreGasRule Eip2200 =
            Eip2200NetMeteredSstoreGasRule.WithTheEip2200GasStipend(IstanbulSload);

        private static readonly ISstoreGasRule Eip2929 =
            Eip2200NetMeteredSstoreGasRule.AsReboundByEip2929();

        [Fact]
        public void Given_CurrentEqualsNewValue_When_Priced_Then_EachBindingChargesItsSloadGas()
        {
            var noOp = Slot(original: 7, current: 7, newValue: 7, isColdAccess: false);

            Assert.Equal(ConstantinopleSload, Eip1283.GetGasCost(noOp));
            Assert.Equal(IstanbulSload, Eip2200.GetGasCost(noOp));
            Assert.Equal(GasConstants.SSTORE_NOOP, Eip2929.GetGasCost(noOp));
        }

        [Fact]
        public void Given_ACleanSlotThatWasOriginallyZero_When_Priced_Then_EachBindingChargesSstoreSet()
        {
            var filledFromZero = Slot(original: 0, current: 0, newValue: 9, isColdAccess: false);

            Assert.Equal(GasConstants.SSTORE_SET, Eip1283.GetGasCost(filledFromZero));
            Assert.Equal(GasConstants.SSTORE_SET, Eip2200.GetGasCost(filledFromZero));
            Assert.Equal(GasConstants.SSTORE_SET, Eip2929.GetGasCost(filledFromZero));
        }

        [Fact]
        public void Given_ACleanSlotThatWasOriginallyNonZero_When_Priced_Then_Eip2929ChargesTheReboundReset()
        {
            var overwritten = Slot(original: 7, current: 7, newValue: 9, isColdAccess: false);

            Assert.Equal(GasConstants.SSTORE_RESET_PRE_BERLIN, Eip1283.GetGasCost(overwritten));
            Assert.Equal(GasConstants.SSTORE_RESET_PRE_BERLIN, Eip2200.GetGasCost(overwritten));
            Assert.Equal(GasConstants.SSTORE_RESET, Eip2929.GetGasCost(overwritten));
        }

        [Fact]
        public void Given_ADirtySlot_When_Priced_Then_EachBindingChargesItsSloadGas()
        {
            var dirty = Slot(original: 7, current: 9, newValue: 11, isColdAccess: false);

            Assert.Equal(ConstantinopleSload, Eip1283.GetGasCost(dirty));
            Assert.Equal(IstanbulSload, Eip2200.GetGasCost(dirty));
            Assert.Equal(GasConstants.SSTORE_NOOP, Eip2929.GetGasCost(dirty));
        }

        [Fact]
        public void Given_AColdSlot_When_Priced_Then_OnlyTheEip2929BindingAddsASurcharge()
        {
            var cold = Slot(original: 0, current: 0, newValue: 9, isColdAccess: true);
            var warm = Slot(original: 0, current: 0, newValue: 9, isColdAccess: false);

            Assert.Equal(Eip1283.GetGasCost(warm), Eip1283.GetGasCost(cold));
            Assert.Equal(Eip2200.GetGasCost(warm), Eip2200.GetGasCost(cold));
            Assert.Equal(Eip2929.GetGasCost(warm) + GasConstants.COLD_SLOAD_COST, Eip2929.GetGasCost(cold));
        }

        [Fact]
        public void Given_GasRemainingAtTheCallStipend_When_Asked_Then_OnlyTheEip2200BindingRefuses()
        {
            Assert.True(Eip2200.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND));
            Assert.False(Eip1283.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND));
            Assert.False(Eip2929.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND));
        }

        [Fact]
        public void Given_GasRemainingJustAboveTheCallStipend_When_Asked_Then_NoBindingRefuses()
        {
            Assert.False(Eip2200.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND + 1));
            Assert.False(Eip1283.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND + 1));
            Assert.False(Eip2929.RefusesTheFrameBeforeReadingTheSlot(GasConstants.CALL_STIPEND + 1));
        }

        private static SstoreSlotState Slot(byte original, byte current, byte newValue, bool isColdAccess) =>
            SstoreSlotState.Resolved(Word(original), Word(current), Word(newValue), isColdAccess);

        private static byte[] Word(byte value)
        {
            var word = new byte[32];
            word[31] = value;
            return word;
        }
    }
}
