using System.Collections.Generic;
using Nethereum.EVM.Execution;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockAccessListStructureRuleTests
    {
        private const int BlockTransactionCount = 10;

        private static BlockAccessListStructureCheck Check(List<AccountChanges> list) =>
            BlockAccessListStructureRule.FindViolation(list, BlockTransactionCount);

        private static string Address(byte last) =>
            BlockAccessListValueConventions.NormaliseAddress("0x" + last.ToString("x2").PadLeft(40, '0'));

        private static AccountChanges Account(byte last) => new AccountChanges(Address(last));

        private static SlotChanges Slot(ulong slot, params ulong[] atIndexes)
        {
            var changes = new SlotChanges(new EvmUInt256(slot));
            foreach (var index in atIndexes)
                changes.Changes.Add(new StorageChange(index, new EvmUInt256(0x42)));
            return changes;
        }

        private static List<AccountChanges> ListOf(params AccountChanges[] accounts) =>
            new List<AccountChanges>(accounts);

        private static void AssertViolation(
            BlockAccessListStructureViolation expected, BlockAccessListStructureCheck actual)
        {
            Assert.Equal(expected, actual.Violation);
            Assert.False(actual.IsWellFormed);
            Assert.NotNull(actual.Description);
        }


        [Fact]
        public void Given_AListOurOwnBuilderProduced_When_Validated_Then_ItPasses()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 1 };
            builder.EnsureAccount("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF");
            builder.AddStorageWrite(Address(0x02), new EvmUInt256(9), new EvmUInt256(1));
            builder.AddStorageWrite(Address(0x02), new EvmUInt256(3), new EvmUInt256(1));
            builder.AddStorageRead(Address(0x02), new EvmUInt256(7));
            builder.AddBalanceChange(Address(0x01), new EvmUInt256(5));
            builder.AddNonceChange(Address(0x01), 1);
            builder.AddCodeChange(Address(0x03), new byte[] { 0x60, 0x00 });
            builder.BlockAccessIndex = 2;
            builder.AddBalanceChange(Address(0x01), new EvmUInt256(6));
            builder.AddNonceChange(Address(0x01), 2);
            builder.AddCodeChange(Address(0x03), new byte[] { 0x60, 0x01 });
            builder.AddStorageWrite(Address(0x02), new EvmUInt256(3), new EvmUInt256(2));

            Assert.True(Check(builder.Build()).IsWellFormed);
        }

        [Fact]
        public void Given_ANullList_When_Validated_Then_ItIsWellFormed()
        {
            Assert.True(BlockAccessListStructureRule
                .FindViolation(null, BlockTransactionCount).IsWellFormed);
        }

        [Fact]
        public void Given_AnEmptyList_When_Validated_Then_ItIsWellFormed()
        {
            Assert.True(Check(new List<AccountChanges>()).IsWellFormed);
        }

        [Fact]
        public void Given_AscendingDistinctAccounts_When_Validated_Then_ItPasses()
        {
            Assert.True(Check(ListOf(Account(0x01), Account(0x02), Account(0x03))).IsWellFormed);
        }


        [Fact]
        public void Given_AccountsInDescendingOrder_When_Validated_Then_Rejected()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountsOutOfOrder,
                Check(ListOf(Account(0x02), Account(0x01))));
        }

        [Fact]
        public void Given_TheSameAccountTwice_When_Validated_Then_Rejected()
        {
            AssertViolation(BlockAccessListStructureViolation.DuplicateAccount,
                Check(ListOf(Account(0x01), Account(0x01))));
        }


        [Fact]
        public void Given_AChecksummedAddress_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0xFFfFfFffFFfffFFfFFfFFFFFffFFFffffFfFFFfF"))));
        }

        [Fact]
        public void Given_AnUnprefixedAddress_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0000000000000000000000000000000000000001"))));
        }

        [Fact]
        public void Given_AnUnpaddedAddress_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0x1"))));
        }

        [Fact]
        public void Given_AnOverLongAddress_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0x" + new string('a', 64)))));
        }

        [Fact]
        public void Given_ANonHexAddress_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0x" + new string('z', 40)))));
        }

        [Fact]
        public void Given_ANullAddress_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges((string)null))));
        }


        [Fact]
        public void Given_ChangedSlotsOutOfOrder_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(2, 1));
            account.StorageChanges.Add(Slot(1, 1));

            AssertViolation(BlockAccessListStructureViolation.ChangedSlotsOutOfOrder,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_TheSameChangedSlotTwice_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1, 1));
            account.StorageChanges.Add(Slot(1, 2));

            AssertViolation(BlockAccessListStructureViolation.DuplicateChangedSlot,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_ASlotChangesEntryWithNoChanges_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1));

            AssertViolation(BlockAccessListStructureViolation.SlotCarriesNoChange,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_StorageChangeIndexesWithinASlotOutOfOrder_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1, 3, 2));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexesOutOfOrder,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_TheSameIndexTwiceWithinASlot_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1, 2, 2));

            AssertViolation(BlockAccessListStructureViolation.DuplicateChangeIndex,
                Check(ListOf(account)));
        }


        [Fact]
        public void Given_ReadSlotsOutOfOrder_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageReads.Add(new EvmUInt256(2));
            account.StorageReads.Add(new EvmUInt256(1));

            AssertViolation(BlockAccessListStructureViolation.ReadSlotsOutOfOrder,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_TheSameReadSlotTwice_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageReads.Add(new EvmUInt256(1));
            account.StorageReads.Add(new EvmUInt256(1));

            AssertViolation(BlockAccessListStructureViolation.DuplicateReadSlot,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_ASlotInBothChangesAndReads_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1, 1));
            account.StorageReads.Add(new EvmUInt256(1));

            AssertViolation(BlockAccessListStructureViolation.SlotBothChangedAndRead,
                Check(ListOf(account)));
        }


        [Fact]
        public void Given_BalanceChangeIndexesOutOfOrder_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.BalanceChanges.Add(new BalanceChange(2, new EvmUInt256(1)));
            account.BalanceChanges.Add(new BalanceChange(1, new EvmUInt256(2)));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexesOutOfOrder,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_DuplicateBalanceChangeIndex_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.BalanceChanges.Add(new BalanceChange(1, new EvmUInt256(1)));
            account.BalanceChanges.Add(new BalanceChange(1, new EvmUInt256(2)));

            AssertViolation(BlockAccessListStructureViolation.DuplicateChangeIndex,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_NonceChangeIndexesOutOfOrder_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.NonceChanges.Add(new NonceChange(2, 1));
            account.NonceChanges.Add(new NonceChange(1, 2));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexesOutOfOrder,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_DuplicateNonceChangeIndex_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.NonceChanges.Add(new NonceChange(1, 1));
            account.NonceChanges.Add(new NonceChange(1, 2));

            AssertViolation(BlockAccessListStructureViolation.DuplicateChangeIndex,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_CodeChangeIndexesOutOfOrder_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.CodeChanges.Add(new CodeChange(2, new byte[] { 0x01 }));
            account.CodeChanges.Add(new CodeChange(1, new byte[] { 0x02 }));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexesOutOfOrder,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_DuplicateCodeChangeIndex_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.CodeChanges.Add(new CodeChange(1, new byte[] { 0x01 }));
            account.CodeChanges.Add(new CodeChange(1, new byte[] { 0x02 }));

            AssertViolation(BlockAccessListStructureViolation.DuplicateChangeIndex,
                Check(ListOf(account)));
        }


        [Fact]
        public void Given_ABalanceChangeIndexAboveTheBlockBound_When_Validated_Then_Rejected()
        {
            // EIP-7928: indices "MUST never be higher than len(transactions) + 1".
            var account = Account(0x01);
            account.BalanceChanges.Add(new BalanceChange(BlockTransactionCount + 2, new EvmUInt256(1)));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_AStorageChangeIndexAboveTheBlockBound_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1, BlockTransactionCount + 2));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_AnIndexExactlyAtTheBlockBound_When_Validated_Then_ItPasses()
        {
            var account = Account(0x01);
            account.BalanceChanges.Add(new BalanceChange(BlockTransactionCount + 1, new EvmUInt256(1)));

            Assert.True(Check(ListOf(account)).IsWellFormed);
        }


        [Fact]
        public void Given_AWellFormedAccountWithEveryFieldPopulated_When_Validated_Then_ItPasses()
        {
            var account = Account(0x01);
            account.StorageChanges.Add(Slot(1, 1, 2));
            account.StorageChanges.Add(Slot(5, 3));
            account.StorageReads.Add(new EvmUInt256(2));
            account.StorageReads.Add(new EvmUInt256(9));
            account.BalanceChanges.Add(new BalanceChange(1, new EvmUInt256(10)));
            account.BalanceChanges.Add(new BalanceChange(2, new EvmUInt256(20)));
            account.NonceChanges.Add(new NonceChange(1, 1));
            account.NonceChanges.Add(new NonceChange(3, 2));
            account.CodeChanges.Add(new CodeChange(2, new byte[] { 0x60, 0x00 }));
            account.CodeChanges.Add(new CodeChange(4, new byte[] { 0x60, 0x01 }));

            Assert.True(Check(ListOf(account)).IsWellFormed);
        }


        [Fact]
        public void Given_ANonceChangeIndexAboveTheBlockBound_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.NonceChanges.Add(new NonceChange(BlockTransactionCount + 2, 1));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_ACodeChangeIndexAboveTheBlockBound_When_Validated_Then_Rejected()
        {
            var account = Account(0x01);
            account.CodeChanges.Add(new CodeChange(BlockTransactionCount + 2, new byte[] { 0x01 }));

            AssertViolation(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                Check(ListOf(account)));
        }

        [Fact]
        public void Given_TheOrderBreaksBetweenTheSecondAndThirdAccounts_When_Validated_Then_Rejected()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountsOutOfOrder,
                Check(ListOf(Account(0x01), Account(0x03), Account(0x02))));
        }

        [Fact]
        public void Given_AChangeAtIndexZero_When_Validated_Then_ItPasses()
        {
            var account = Account(0x01);
            account.BalanceChanges.Add(new BalanceChange(0, new EvmUInt256(1)));

            Assert.True(Check(ListOf(account)).IsWellFormed);
        }

        [Fact]
        public void Given_ABlockWithNoTransactions_When_Validated_Then_OnlyIndexesZeroAndOneAreAllowed()
        {
            var postExecution = Account(0x01);
            postExecution.BalanceChanges.Add(new BalanceChange(1, new EvmUInt256(1)));
            Assert.True(BlockAccessListStructureRule
                .FindViolation(ListOf(postExecution), 0).IsWellFormed);

            var beyond = Account(0x01);
            beyond.BalanceChanges.Add(new BalanceChange(2, new EvmUInt256(1)));
            AssertViolation(BlockAccessListStructureViolation.ChangeIndexAboveBlockBound,
                BlockAccessListStructureRule.FindViolation(ListOf(beyond), 0));
        }

        [Fact]
        public void Given_ANegativeTransactionCount_When_Validated_Then_ItThrowsRatherThanBoundingToZero()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                BlockAccessListStructureRule.FindViolation(ListOf(Account(0x01)), -1));
        }

        [Fact]
        public void Given_AnUppercasePrefix_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0X" + new string('a', 40)))));
        }

        [Fact]
        public void Given_AnAddressWithEmbeddedWhitespace_When_Validated_Then_RejectedAsNonCanonical()
        {
            AssertViolation(BlockAccessListStructureViolation.AccountAddressNotCanonical,
                Check(ListOf(new AccountChanges("0x" + new string('a', 39) + " "))));
        }

        [Fact]
        public void Given_AViolationInTheSecondAccount_When_Validated_Then_ItIsStillFound()
        {
            var clean = Account(0x01);
            var broken = Account(0x02);
            broken.StorageReads.Add(new EvmUInt256(2));
            broken.StorageReads.Add(new EvmUInt256(1));

            AssertViolation(BlockAccessListStructureViolation.ReadSlotsOutOfOrder,
                Check(ListOf(clean, broken)));
        }
    }
}
