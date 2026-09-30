using System.Collections.Generic;
using Nethereum.EVM.Gas;
using Nethereum.Model;

namespace Nethereum.EVM.Execution
{
    /// <summary>
    /// EIP-7928: "The block access list is constrained by the block gas limit
    /// rather than a fixed maximum number of items. The constraint is defined as:
    /// <c>bal_items &lt;= block_gas_limit // ITEM_COST</c>", where
    /// "<c>bal_items = storage_keys + addresses</c>" and "<c>ITEM_COST = 2000</c>".
    ///
    /// <para>Applied to the list a block BUILT, so everything that reaches the list
    /// counts against the bound — including the accounts touched by system calls and
    /// withdrawals, which consume no block gas of their own.</para>
    /// </summary>
    public static class BlockAccessListSizeRule
    {
        /// <summary>
        /// EIP-7928: "The <c>storage_keys</c> is the total number of storage keys
        /// across all accounts, and <c>addresses</c> is the total number of unique
        /// addresses accessed in the block."
        ///
        /// <para>Summing an account's changed and read slots counts each slot once
        /// because <see cref="BlockAccessListBuilder.Build"/> emits a slot that
        /// changed as a change and never also as a read.</para>
        /// </summary>
        public static long CountItems(List<AccountChanges> blockAccessList)
        {
            if (blockAccessList == null) return 0;

            long items = 0;
            for (var i = 0; i < blockAccessList.Count; i++)
            {
                var account = blockAccessList[i];
                items += 1 + account.StorageChanges.Count + account.StorageReads.Count;
            }
            return items;
        }

        public static bool ExceedsBlockGasLimit(List<AccountChanges> blockAccessList, long blockGasLimit) =>
            CountItems(blockAccessList) > blockGasLimit / GasConstants.EIP7928_ITEM_COST;
    }
}
