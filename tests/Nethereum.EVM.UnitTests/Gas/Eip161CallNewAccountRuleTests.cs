using System;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip161CallNewAccountRuleTests
    {
        private static CallTransfer Carrying(long value) =>
            CallTransfer.OfTheValueAlone(new EvmUInt256(value));

        [Fact]
        public void Given_ACallCarryingNoValue_When_PricedAtEip161_Then_NoNewAccountIsCreatedWithoutTheTargetBeingRead()
        {
            var verdict = Eip161CallNewAccountRule.AsIntroducedByEip161().JudgeTheTransfer(Carrying(0));

            Assert.Equal(CallNewAccountVerdict.NoNewAccountIsCreated, verdict);
        }

        [Fact]
        public void Given_ACallCarryingValueAndAnUnreadTarget_When_PricedAtEip161_Then_ItAsksForTheTarget()
        {
            var verdict = Eip161CallNewAccountRule.AsIntroducedByEip161().JudgeTheTransfer(Carrying(1));

            Assert.Equal(CallNewAccountVerdict.NotUntilTheTargetDeadnessIsKnown, verdict);
        }

        [Fact]
        public void Given_ACallCarryingValueIntoADeadTarget_When_PricedAtEip161_Then_ItBringsTheTargetIntoExistence()
        {
            var verdict = Eip161CallNewAccountRule.AsIntroducedByEip161()
                .JudgeTheTransfer(Carrying(1).AndTheTargetDeadnessPerEip161(targetIsDead: true));

            Assert.Equal(CallNewAccountVerdict.TheTransferBringsTheTargetIntoExistence, verdict);
        }

        [Fact]
        public void Given_ACallCarryingValueIntoALiveTarget_When_PricedAtEip161_Then_NoNewAccountIsCreated()
        {
            var verdict = Eip161CallNewAccountRule.AsIntroducedByEip161()
                .JudgeTheTransfer(Carrying(1).AndTheTargetDeadnessPerEip161(targetIsDead: false));

            Assert.Equal(CallNewAccountVerdict.NoNewAccountIsCreated, verdict);
        }

        [Fact]
        public void Given_ACallCarryingNoValueIntoADeadTarget_When_PricedAtEip161_Then_NoNewAccountIsCreated()
        {
            var verdict = Eip161CallNewAccountRule.AsIntroducedByEip161()
                .JudgeTheTransfer(Carrying(0).AndTheTargetDeadnessPerEip161(targetIsDead: true));

            Assert.Equal(CallNewAccountVerdict.NoNewAccountIsCreated, verdict);
        }

        [Theory]
        [InlineData(false, 2600L)]
        [InlineData(true, 2600L + GasConstants.CALL_VALUE_TRANSFER)]
        public void Given_AnAccessAndMemoryPrice_When_Eip161PricesPreStateValidation_Then_TheValueTransferIsAddedOnlyForAValueCall(
            bool carriesValue, long expected)
        {
            var gas = Eip161CallNewAccountRule.AsIntroducedByEip161()
                .PreStateValidationGas(accessCost: 2600, memoryExpansionCost: 0, carriesValue: carriesValue);

            Assert.Equal(expected, gas);
        }

        [Fact]
        public void Given_MemoryExpansion_When_Eip161PricesPreStateValidation_Then_ItIsAddedToTheAccessPrice()
        {
            var gas = Eip161CallNewAccountRule.AsIntroducedByEip161()
                .PreStateValidationGas(accessCost: 700, memoryExpansionCost: 512, carriesValue: false);

            Assert.Equal(1212L, gas);
        }

        [Fact]
        public void Given_TheEip161Binding_When_TheNewAccountChargeIsRead_Then_ItIsExecutionGasAlone()
        {
            var rule = Eip161CallNewAccountRule.AsIntroducedByEip161();

            Assert.Equal(GasConstants.CALL_NEW_ACCOUNT, rule.NewAccountExecutionGas);
            Assert.Equal(0L, rule.NewAccountStateGas);
        }

        [Fact]
        public void Given_TheEip8038Binding_When_TheNewAccountChargeIsRead_Then_ItIsStateGasAlone()
        {
            var rule = Eip161CallNewAccountRule.AsReboundByEip8038();

            Assert.Equal(0L, rule.NewAccountExecutionGas);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, rule.NewAccountStateGas);
        }

        [Fact]
        public void Given_TheEip8038Binding_When_AValueCallIsPricedForPreStateValidation_Then_ItPaysTheRepricedTransfer()
        {
            var gas = Eip161CallNewAccountRule.AsReboundByEip8038()
                .PreStateValidationGas(accessCost: 3000, memoryExpansionCost: 0, carriesValue: true);

            Assert.Equal(3000L + GasConstants.EIP8038_CALL_VALUE_TRANSFER, gas);
        }


        [Fact]
        public void Given_AnUnresolvedTransfer_When_Priced_Then_ItIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Eip161CallNewAccountRule.AsIntroducedByEip161().JudgeTheTransfer(default(CallTransfer)));
        }

        [Fact]
        public void Given_AnUnresolvedTransfer_When_AnyPredicateIsAsked_Then_ItIsRejected()
        {
            var unresolved = default(CallTransfer);

            Assert.Throws<InvalidOperationException>(() => { var _ = unresolved.CarriesValue; });
            Assert.Throws<InvalidOperationException>(() => { var _ = unresolved.TheTargetDeadnessIsKnown; });
            Assert.Throws<InvalidOperationException>(() => { var _ = unresolved.TheTargetIsDeadPerEip161; });
            Assert.Throws<InvalidOperationException>(() => unresolved.AndTheTargetDeadnessPerEip161(targetIsDead: true));
        }

        [Fact]
        public void Given_ATransferResolvedForItsValueAlone_When_TheTargetIsAskedFor_Then_ItIsRejected()
        {
            var transfer = Carrying(1);

            Assert.True(transfer.CarriesValue);
            Assert.False(transfer.TheTargetDeadnessIsKnown);
            Assert.Throws<InvalidOperationException>(() => { var _ = transfer.TheTargetIsDeadPerEip161; });
        }

        [Fact]
        public void Given_ATransfer_When_TheTargetIsResolved_Then_TheValueItCarriesIsUnchanged()
        {
            var resolved = Carrying(1).AndTheTargetDeadnessPerEip161(targetIsDead: true);

            Assert.True(resolved.CarriesValue);
            Assert.True(resolved.TheTargetDeadnessIsKnown);
            Assert.True(resolved.TheTargetIsDeadPerEip161);
        }
    }
}
