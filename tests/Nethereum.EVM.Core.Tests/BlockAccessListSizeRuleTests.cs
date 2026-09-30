using System.Collections.Generic;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.Core.Tests
{
    /// <summary>
    /// AMS-7928-05. EIP-7928: "<c>bal_items &lt;= block_gas_limit // ITEM_COST</c>",
    /// where "<c>bal_items = storage_keys + addresses</c>" and "<c>ITEM_COST = 2000</c>".
    /// </summary>
    public class BlockAccessListSizeRuleTests
    {
        private const long ItemCost = GasConstants.EIP7928_ITEM_COST;

        private static List<AccountChanges> ListOfAccounts(int accounts)
        {
            var blockAccessList = new List<AccountChanges>(accounts);
            for (var i = 0; i < accounts; i++)
                blockAccessList.Add(new AccountChanges("0x" + (0x10000 + i).ToString("x40")));
            return blockAccessList;
        }

        [Fact]
        [Trait("Rule", "AMS-7928-05")]
        public void Given_ABlockAccessListAtTheItemBound_When_Measured_Then_ItIsWithinTheBlockGasLimit()
        {
            var blockAccessList = ListOfAccounts(8);

            Assert.False(BlockAccessListSizeRule.ExceedsBlockGasLimit(blockAccessList, 8 * ItemCost));
        }

        [Fact]
        [Trait("Rule", "AMS-7928-05")]
        public void Given_ABlockAccessListOneItemOverTheBound_When_Measured_Then_ItExceedsTheBlockGasLimit()
        {
            var blockAccessList = ListOfAccounts(9);

            Assert.True(BlockAccessListSizeRule.ExceedsBlockGasLimit(blockAccessList, 8 * ItemCost));
        }

        [Fact]
        [Trait("Rule", "AMS-7928-05")]
        public void Given_AnEmptyBlockAccessList_When_Measured_Then_ItIsWithinAnyBlockGasLimit()
        {
            Assert.False(BlockAccessListSizeRule.ExceedsBlockGasLimit(new List<AccountChanges>(), 0));
            Assert.False(BlockAccessListSizeRule.ExceedsBlockGasLimit(null, 0));
        }

        [Fact]
        [Trait("Rule", "AMS-7928-05")]
        public void Given_AnAccountWithChangedAndReadSlots_When_ItemsAreCounted_Then_TheAddressAndEverySlotCountOnce()
        {
            var account = new AccountChanges("0x0000000000000000000000000000000000001234");
            account.StorageChanges.Add(new SlotChanges(new EvmUInt256(1)));
            account.StorageChanges.Add(new SlotChanges(new EvmUInt256(2)));
            account.StorageReads.Add(new EvmUInt256(3));

            Assert.Equal(4, BlockAccessListSizeRule.CountItems(new List<AccountChanges> { account }));
        }

        [Fact]
        [Trait("Rule", "AMS-7928-05")]
        public void Given_ASlotWrittenSeveralTimes_When_ItemsAreCounted_Then_ItCountsOnce()
        {
            var builder = new BlockAccessListBuilder { BlockAccessIndex = 1 };
            builder.AddStorageWrite("0x0000000000000000000000000000000000001234", new EvmUInt256(7), new EvmUInt256(1));
            builder.BlockAccessIndex = 2;
            builder.AddStorageWrite("0x0000000000000000000000000000000000001234", new EvmUInt256(7), new EvmUInt256(2));
            builder.AddStorageRead("0x0000000000000000000000000000000000001234", new EvmUInt256(7));

            Assert.Equal(2, BlockAccessListSizeRule.CountItems(builder.Build()));
        }
    }
}
