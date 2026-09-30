using System;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class SelfDestructBeneficiaryRuleTests
    {
        private static readonly byte[] SomeCode = { 0x60, 0x00 };

        private static SelfDestructSweep SweepTowardsAnEmptyBeneficiary() =>
            SelfDestructSweep.OfTheBeneficiaryAlone(
                AccountExistenceFacts.Resolved(new EvmUInt256(0), null, new EvmUInt256(0), hasAnAccountRecord: false));

        private static SelfDestructSweep SweepTowards(
            long balance, byte[] code, long nonce, bool hasAnAccountRecord) =>
            SelfDestructSweep.OfTheBeneficiaryAlone(
                AccountExistenceFacts.Resolved(
                    new EvmUInt256(balance), code, new EvmUInt256(nonce), hasAnAccountRecord));

        public static TheoryData<string, ISelfDestructBeneficiaryRule> EveryEip161Binding() =>
            new TheoryData<string, ISelfDestructBeneficiaryRule>
            {
                { "EIP-161", Eip161SelfDestructBeneficiaryRule.AsIntroducedByEip161() },
                { "EIP-2929", Eip161SelfDestructBeneficiaryRule.WithTheEip2929AccessList() },
                { "EIP-8038", Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038() }
            };

        [Theory]
        [MemberData(nameof(EveryEip161Binding))]
        public void Given_BeneficiaryEmptyPerEip161AndContractHasBalance_When_SelfDestructPriced_Then_ChargesNewAccount(
            string binding, ISelfDestructBeneficiaryRule rule)
        {
            var sweep = SweepTowardsAnEmptyBeneficiary().AndTheContractBalance(new EvmUInt256(1));

            Assert.Equal(SelfDestructSweepVerdict.TheSweepBringsTheBeneficiaryIntoExistence, rule.JudgeTheSweep(sweep));
        }

        [Theory]
        [MemberData(nameof(EveryEip161Binding))]
        public void Given_BeneficiaryEmptyButContractHasNoBalance_When_SelfDestructPriced_Then_ChargesNoNewAccount(
            string binding, ISelfDestructBeneficiaryRule rule)
        {
            var sweep = SweepTowardsAnEmptyBeneficiary().AndTheContractBalance(new EvmUInt256(0));

            Assert.Equal(SelfDestructSweepVerdict.NoNewAccountIsCreated, rule.JudgeTheSweep(sweep));
        }

        [Theory]
        [MemberData(nameof(EveryEip161Binding))]
        public void Given_ABeneficiaryThatIsAlreadyAlive_When_SelfDestructPriced_Then_TheContractBalanceIsNeverAskedFor(
            string binding, ISelfDestructBeneficiaryRule rule)
        {
            Assert.Equal(SelfDestructSweepVerdict.NoNewAccountIsCreated,
                rule.JudgeTheSweep(SweepTowards(balance: 5, code: null, nonce: 0, hasAnAccountRecord: false)));
            Assert.Equal(SelfDestructSweepVerdict.NoNewAccountIsCreated,
                rule.JudgeTheSweep(SweepTowards(balance: 0, code: SomeCode, nonce: 0, hasAnAccountRecord: false)));
            Assert.Equal(SelfDestructSweepVerdict.NoNewAccountIsCreated,
                rule.JudgeTheSweep(SweepTowards(balance: 0, code: null, nonce: 3, hasAnAccountRecord: false)));
        }

        [Theory]
        [MemberData(nameof(EveryEip161Binding))]
        public void Given_ADeadBeneficiaryAndNoContractBalanceRead_When_SelfDestructPriced_Then_TheRuleAsksForIt(
            string binding, ISelfDestructBeneficiaryRule rule)
        {
            Assert.Equal(SelfDestructSweepVerdict.NotUntilTheContractBalanceIsKnown,
                rule.JudgeTheSweep(SweepTowardsAnEmptyBeneficiary()));
        }

        /// <summary>
        /// EIP-2929: "Note: <c>SELFDESTRUCT</c> does not charge a
        /// <c>WARM_STORAGE_READ_COST</c> in case the recipient is already warm,
        /// which differs from how the other call-variants work."
        /// </summary>
        [Fact]
        public void Given_WarmBeneficiary_When_SelfDestructPriced_Then_AddsNoWarmAccessCost()
        {
            var rule = Eip161SelfDestructBeneficiaryRule.WithTheEip2929AccessList();

            Assert.Equal(GasConstants.SELFDESTRUCT_COST, rule.PreStateValidationGas(isColdAccess: false));
        }

        [Fact]
        public void Given_ColdBeneficiary_When_SelfDestructPriced_Then_AddsColdAccountAccessOnTopOfSelfDestructCost()
        {
            var rule = Eip161SelfDestructBeneficiaryRule.WithTheEip2929AccessList();

            Assert.Equal(GasConstants.SELFDESTRUCT_COST + GasConstants.COLD_ACCOUNT_ACCESS_COST,
                rule.PreStateValidationGas(isColdAccess: true));
        }

        [Fact]
        public void Given_TheEip8038Binding_When_AColdBeneficiaryIsPriced_Then_ItTakesTheRepricedAccessCost()
        {
            var rule = Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038();

            Assert.Equal(GasConstants.SELFDESTRUCT_COST + GasConstants.EIP8038_COLD_ACCOUNT_ACCESS,
                rule.PreStateValidationGas(isColdAccess: true));
        }

        [Fact]
        public void Given_TheEip8038Binding_When_TheSweepCreatesTheBeneficiary_Then_BothTheAccountWriteAndTheStateGasAreOwed()
        {
            var rule = Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038();

            Assert.Equal(GasConstants.EIP8038_ACCOUNT_WRITE, rule.NewAccountExecutionGas);
            Assert.Equal(GasConstants.EIP8037_NEW_ACCOUNT_STATE_GAS, rule.NewAccountStateGas);
        }

        [Theory]
        [InlineData("EIP-161", false)]
        [InlineData("EIP-2929", true)]
        [InlineData("EIP-8038", true)]
        public void Given_AnEip161Binding_When_AskedWhetherTheBeneficiaryJoinsTheAccessList_Then_OnlyBerlinOnwardSaysYes(
            string binding, bool expected)
        {
            var rule = binding == "EIP-161" ? Eip161SelfDestructBeneficiaryRule.AsIntroducedByEip161()
                     : binding == "EIP-2929" ? Eip161SelfDestructBeneficiaryRule.WithTheEip2929AccessList()
                     : Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038();

            Assert.Equal(expected, rule.TheBeneficiaryEntersTheAccessList);
        }

        [Fact]
        public void Given_PreEip161ForkAndBeneficiaryHasAnAccountRecord_When_SelfDestructPriced_Then_ChargesNoNewAccount()
        {
            var sweep = SweepTowards(balance: 0, code: null, nonce: 0, hasAnAccountRecord: true);

            Assert.Equal(SelfDestructSweepVerdict.NoNewAccountIsCreated,
                PreEip161SelfDestructBeneficiaryRule.Instance.JudgeTheSweep(sweep));
        }

        [Fact]
        public void Given_PreEip161ForkAndBeneficiaryHasNoAccountRecord_When_SelfDestructPriced_Then_ChargesNewAccount()
        {
            var sweep = SweepTowards(balance: 0, code: null, nonce: 0, hasAnAccountRecord: false);

            Assert.Equal(SelfDestructSweepVerdict.TheSweepBringsTheBeneficiaryIntoExistence,
                PreEip161SelfDestructBeneficiaryRule.Instance.JudgeTheSweep(sweep));
        }

        [Fact]
        public void Given_TwoBeneficiariesDifferingOnlyInWhatTheyHold_When_PricedBeforeEip161_Then_TheVerdictIsTheSame()
        {
            var rich = SweepTowards(balance: 99, code: SomeCode, nonce: 7, hasAnAccountRecord: true);
            var bare = SweepTowards(balance: 0, code: null, nonce: 0, hasAnAccountRecord: true);

            Assert.Equal(PreEip161SelfDestructBeneficiaryRule.Instance.JudgeTheSweep(bare),
                PreEip161SelfDestructBeneficiaryRule.Instance.JudgeTheSweep(rich));
        }

        [Fact]
        public void Given_PreEip161Fork_When_TheBeneficiaryIsCold_Then_ThereIsNoAccessSurchargeToAdd()
        {
            Assert.Equal(GasConstants.SELFDESTRUCT_COST,
                PreEip161SelfDestructBeneficiaryRule.Instance.PreStateValidationGas(isColdAccess: true));
            Assert.False(PreEip161SelfDestructBeneficiaryRule.Instance.TheBeneficiaryEntersTheAccessList);
        }

        [Fact]
        public void Given_TheSameSweepAndRule_When_JudgedTwice_Then_TheVerdictIsIdentical()
        {
            var rule = Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038();
            var sweep = SweepTowardsAnEmptyBeneficiary().AndTheContractBalance(new EvmUInt256(1));

            Assert.Equal(rule.JudgeTheSweep(sweep), rule.JudgeTheSweep(sweep));
        }
    }

    public class SelfDestructSweepResolutionTests
    {
        [Fact]
        public void Given_AnUnresolvedSweep_When_AnyPredicateIsAsked_Then_ItIsRejected()
        {
            var unresolved = default(SelfDestructSweep);

            Assert.Throws<InvalidOperationException>(() => unresolved.TheBeneficiaryIsEmptyPerEip161);
            Assert.Throws<InvalidOperationException>(() => unresolved.TheBeneficiaryHasAnAccountRecord);
            Assert.Throws<InvalidOperationException>(() => unresolved.TheSweepCarriesValue);
            Assert.Throws<InvalidOperationException>(() => unresolved.TheContractBalanceIsKnown);
            Assert.Throws<InvalidOperationException>(() => unresolved.AndTheContractBalance(new EvmUInt256(1)));
        }

        [Fact]
        public void Given_AnUnresolvedSweep_When_Judged_Then_ItIsRejected()
        {
            Assert.Throws<InvalidOperationException>(
                () => Eip161SelfDestructBeneficiaryRule.AsReboundByEip8038().JudgeTheSweep(default(SelfDestructSweep)));
            Assert.Throws<InvalidOperationException>(
                () => PreEip161SelfDestructBeneficiaryRule.Instance.JudgeTheSweep(default(SelfDestructSweep)));
        }

        [Fact]
        public void Given_AnUnresolvedAccount_When_AnyPredicateIsAsked_Then_ItIsRejected()
        {
            var unresolved = default(AccountExistenceFacts);

            Assert.Throws<InvalidOperationException>(() => unresolved.IsEmptyPerEip161);
            Assert.Throws<InvalidOperationException>(() => unresolved.HasAnAccountRecord);
        }

        [Fact]
        public void Given_ASweepWhoseContractBalanceWasNeverRead_When_AskedWhatItCarries_Then_ItIsRejected()
        {
            var sweep = SelfDestructSweep.OfTheBeneficiaryAlone(
                AccountExistenceFacts.Resolved(new EvmUInt256(0), null, new EvmUInt256(0), hasAnAccountRecord: false));

            Assert.False(sweep.TheContractBalanceIsKnown);
            Assert.Throws<InvalidOperationException>(() => sweep.TheSweepCarriesValue);
        }

        [Fact]
        public void Given_ASweepWhoseContractBalanceWasRead_When_AskedWhatItCarries_Then_ItAnswers()
        {
            var sweep = SelfDestructSweep.OfTheBeneficiaryAlone(
                AccountExistenceFacts.Resolved(new EvmUInt256(0), null, new EvmUInt256(0), hasAnAccountRecord: false));

            Assert.True(sweep.AndTheContractBalance(new EvmUInt256(1)).TheContractBalanceIsKnown);
            Assert.True(sweep.AndTheContractBalance(new EvmUInt256(1)).TheSweepCarriesValue);
            Assert.False(sweep.AndTheContractBalance(new EvmUInt256(0)).TheSweepCarriesValue);
        }

        [Fact]
        public void Given_AFundedAccount_When_Eip161EmptinessIsJudged_Then_ItIsNotEmpty()
        {
            var funded = AccountExistenceFacts.Resolved(
                new EvmUInt256(1), null, new EvmUInt256(0), hasAnAccountRecord: false);

            Assert.False(funded.IsEmptyPerEip161);
        }
    }
}
