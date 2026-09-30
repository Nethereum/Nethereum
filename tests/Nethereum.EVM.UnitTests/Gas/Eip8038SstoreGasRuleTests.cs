using System;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Gas.Opcodes.Rules;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip8038SstoreGasRuleTests
    {
        private const long ColdAccess = GasConstants.COLD_SLOAD_COST;
        private const long WarmAccess = GasConstants.WARM_STORAGE_READ_COST;
        private const long StorageWrite = GasConstants.EIP8038_STORAGE_WRITE;

        [Fact]
        public void Given_AColdSlotWhoseValueDoesNotChange_When_Eip8038SstorePriced_Then_ChargesTheColdAccessComponentAlone()
        {
            var slot = Slot(original: 7, current: 7, newValue: 7, isColdAccess: true);

            Assert.Equal(ColdAccess, Price(slot));
        }

        [Fact]
        public void Given_AWarmSlotWhoseValueDoesNotChange_When_Eip8038SstorePriced_Then_ChargesTheWarmAccessComponentAlone()
        {
            var slot = Slot(original: 7, current: 7, newValue: 7, isColdAccess: false);

            Assert.Equal(WarmAccess, Price(slot));
        }

        [Fact]
        public void Given_TheWriteMovesTheSlotAwayFromItsTransactionStartValue_When_Eip8038SstorePriced_Then_AddsStorageWrite()
        {
            var slot = Slot(original: 7, current: 7, newValue: 9, isColdAccess: true);

            Assert.Equal(ColdAccess + StorageWrite, Price(slot));
        }

        [Fact]
        public void Given_TheSlotHasAlreadyMovedAwayFromItsTransactionStartValue_When_Eip8038SstorePriced_Then_ChargesTheAccessComponentAlone()
        {
            var slot = Slot(original: 7, current: 9, newValue: 11, isColdAccess: false);

            Assert.Equal(WarmAccess, Price(slot));
        }

        [Fact]
        public void Given_TheWriteReturnsTheSlotToItsTransactionStartValue_When_Eip8038SstorePriced_Then_ChargesTheAccessComponentAlone()
        {
            var slot = Slot(original: 7, current: 9, newValue: 7, isColdAccess: false);

            Assert.Equal(WarmAccess, Price(slot));
        }

        [Fact]
        public void Given_ASlotFilledFromZero_When_Eip8038SstorePriced_Then_TheWriteComponentIsFlatRegardlessOfTheOriginalValue()
        {
            var fromZero = Slot(original: 0, current: 0, newValue: 9, isColdAccess: true);
            var fromNonZero = Slot(original: 7, current: 7, newValue: 9, isColdAccess: true);

            Assert.Equal(Price(fromNonZero), Price(fromZero));
        }

        [Fact]
        public void Given_AValueThatIsNotAResolvedWord_When_ASlotStateIsBuilt_Then_ItIsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => SstoreSlotState.Resolved(null, Word(1), Word(1), isColdAccess: false));
            Assert.Throws<ArgumentException>(
                () => SstoreSlotState.Resolved(new byte[20], Word(1), Word(1), isColdAccess: false));
        }

        [Fact]
        public void Given_AnUnresolvedSlotState_When_Priced_Then_ItIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => Price(default(SstoreSlotState)));
            Assert.Throws<InvalidOperationException>(() => Price(new SstoreSlotState()));
        }

        [Fact]
        public void Given_AnUnresolvedSlotState_When_AnyPredicateIsAsked_Then_ItIsRejected()
        {
            var unresolved = default(SstoreSlotState);

            Assert.Throws<InvalidOperationException>(() => unresolved.IsColdAccess);
            Assert.Throws<InvalidOperationException>(() => unresolved.NewValueDiffersFromCurrent);
            Assert.Throws<InvalidOperationException>(() => unresolved.SlotStillHoldsItsTransactionStartValue);
            Assert.Throws<InvalidOperationException>(() => unresolved.OriginalValueIsZero);
            Assert.Throws<InvalidOperationException>(() => unresolved.CurrentValueIsZero);
            Assert.Throws<InvalidOperationException>(() => unresolved.NewValueIsZero);
        }

        private static long Price(SstoreSlotState slot) =>
            Eip8038SstoreGasRule.Instance.GetGasCost(slot);

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
