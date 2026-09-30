using System.Linq;
using Nethereum.EVM.Execution;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    public class BlockAccessListBuilderTests
    {
        private const string A = "0x000000000000000000000000000000000000000a";
        private const string B = "0x000000000000000000000000000000000000000b";

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ASystemCallAndTheFirstTransaction_When_BothWriteTheSameSlot_Then_TheyAreSeparateChangesAtZeroAndOne()
        {
            var builder = new BlockAccessListBuilder();

            builder.BlockAccessIndex = 0;
            builder.AddStorageWrite(A, Slot(1), Value(11));

            builder.BlockAccessIndex = 1;
            builder.AddStorageWrite(A, Slot(1), Value(22));

            var slot = builder.Build().Single().StorageChanges.Single();

            Assert.Equal(2, slot.Changes.Count);
            Assert.Equal(0UL, slot.Changes[0].BlockAccessIndex);
            Assert.Equal(Value(11), slot.Changes[0].PostValue);
            Assert.Equal(1UL, slot.Changes[1].BlockAccessIndex);
            Assert.Equal(Value(22), slot.Changes[1].PostValue);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ASlotWrittenTwiceInOneTransaction_When_Built_Then_OnlyTheFinalValueIsKept()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 3 };
            builder.AddStorageWrite(A, Slot(1), Value(11));
            builder.AddStorageWrite(A, Slot(1), Value(99));

            var slot = builder.Build().Single().StorageChanges.Single();

            var change = Assert.Single(slot.Changes);
            Assert.Equal(3UL, change.BlockAccessIndex);
            Assert.Equal(Value(99), change.PostValue);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ChangesRecordedOutOfIndexOrder_When_Built_Then_EveryChangeSeriesIsSortedByIndex()
        {
            var builder = new BlockAccessListBuilder();

            builder.BlockAccessIndex = 5;
            builder.AddStorageWrite(A, Slot(1), Value(50));
            builder.AddBalanceChange(A, Value(500));
            builder.AddNonceChange(A, 5);

            builder.BlockAccessIndex = 0;
            builder.AddStorageWrite(A, Slot(1), Value(0));
            builder.AddBalanceChange(A, Value(100));
            builder.AddNonceChange(A, 1);

            var account = builder.Build().Single();

            Assert.Equal(new ulong[] { 0, 5 }, account.StorageChanges.Single().Changes.Select(c => c.BlockAccessIndex));
            Assert.Equal(new ulong[] { 0, 5 }, account.BalanceChanges.Select(c => c.BlockAccessIndex));
            Assert.Equal(new ulong[] { 0, 5 }, account.NonceChanges.Select(c => c.BlockAccessIndex));
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AccountsAndSlotsInsertedInReverse_When_Built_Then_BothAreSortedAscending()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 1 };
            builder.AddStorageWrite(B, Slot(2), Value(1));
            builder.AddStorageWrite(A, Slot(9), Value(1));
            builder.AddStorageWrite(A, Slot(2), Value(1));

            var built = builder.Build();

            Assert.Equal(new[] { A, B }, built.Select(a => a.Address));
            Assert.Equal(new[] { Slot(2), Slot(9) }, built[0].StorageChanges.Select(s => s.Slot));
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ASlotThatWasReadAndThenWritten_When_Built_Then_ItAppearsOnlyAsAChange()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 1 };
            builder.AddStorageRead(A, Slot(1));
            builder.AddStorageRead(A, Slot(2));
            builder.AddStorageWrite(A, Slot(1), Value(7));

            var account = builder.Build().Single();

            Assert.Equal(Slot(1), account.StorageChanges.Single().Slot);
            Assert.Equal(new[] { Slot(2) }, account.StorageReads);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_AnAccountOnlyTouched_When_Built_Then_ItIsListedWithNoChanges()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 1 };
            builder.EnsureAccount(A);

            var account = builder.Build().Single();

            Assert.Equal(A, account.Address);
            Assert.Empty(account.StorageChanges);
            Assert.Empty(account.StorageReads);
            Assert.Empty(account.BalanceChanges);
            Assert.Empty(account.NonceChanges);
            Assert.Empty(account.CodeChanges);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_TheSameAddressInDifferentSpellings_When_Built_Then_ItIsOneAccount()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 1 };
            builder.AddStorageWrite("0x000000000000000000000000000000000000000A", Slot(1), Value(1));
            builder.AddBalanceChange("000000000000000000000000000000000000000a", Value(2));
            builder.AddNonceChange(A, 1);

            var account = Assert.Single(builder.Build());
            Assert.Equal(A, account.Address);
            Assert.Single(account.StorageChanges);
            Assert.Single(account.BalanceChanges);
            Assert.Single(account.NonceChanges);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_PostExecutionWorkAfterTwoTransactions_When_Built_Then_ItIsIndexedAboveBoth()
        {
            var builder = new BlockAccessListBuilder();

            builder.BlockAccessIndex = 1;
            builder.AddBalanceChange(A, Value(10));
            builder.BlockAccessIndex = 2;
            builder.AddBalanceChange(A, Value(20));
            builder.BlockAccessIndex = 3;
            builder.AddBalanceChange(A, Value(30));

            var balances = builder.Build().Single().BalanceChanges;

            Assert.Equal(new ulong[] { 1, 2, 3 }, balances.Select(c => c.BlockAccessIndex));
            Assert.Equal(Value(30), balances.Last().PostBalance);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_TwoNonceChangesAtOneIndex_When_Built_Then_TheHighestIsKept()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 2 };
            builder.AddNonceChange(A, 5);
            builder.AddNonceChange(A, 3);

            var change = Assert.Single(builder.Build().Single().NonceChanges);

            Assert.Equal(2UL, change.BlockAccessIndex);
            Assert.Equal(5UL, change.NewNonce);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_TwoBalanceChangesAtOneIndexWithTheSecondLower_When_Built_Then_TheLastIsKeptNotTheHighest()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 2 };
            builder.AddBalanceChange(A, Value(500));
            builder.AddBalanceChange(A, Value(100));

            var change = Assert.Single(builder.Build().Single().BalanceChanges);

            Assert.Equal(Value(100), change.PostBalance);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_TwoCodeChangesAtOneIndex_When_Built_Then_TheLastIsKept()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 2 };
            var first = new byte[] { 0xFF, 0xFF };
            var second = new byte[] { 0x00, 0x01 };
            builder.AddCodeChange(A, first);
            builder.AddCodeChange(A, second);

            var change = Assert.Single(builder.Build().Single().CodeChanges);

            Assert.Same(second, change.NewCode);
        }

        [Fact]
        [Trait("Category", "EIP7928")]
        public void Given_ASlotWrittenTwiceInOneTransactionWithTheSecondLower_When_Built_Then_TheLastValueIsKeptNotTheHighest()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 3 };
            builder.AddStorageWrite(A, Slot(1), Value(99));
            builder.AddStorageWrite(A, Slot(1), Value(11));

            var slot = builder.Build().Single().StorageChanges.Single();
            var change = Assert.Single(slot.Changes);

            Assert.Equal(Value(11), change.PostValue);
        }

        private static EvmUInt256 Slot(int n) => new EvmUInt256(n);
        private static EvmUInt256 Value(int n) => new EvmUInt256(n);
    }
}
