using System.Collections.Generic;

namespace Nethereum.CoreChain.Models
{
    public static class BlockWideLogIndex
    {
        public static void Assign(List<FilteredLog> blockLogs)
        {
            if (blockLogs == null || blockLogs.Count <= 1)
            {
                if (blockLogs != null && blockLogs.Count == 1) blockLogs[0].LogIndex = 0;
                return;
            }

            blockLogs.Sort(CompareByTransactionThenPositionWithinTransaction);
            for (int i = 0; i < blockLogs.Count; i++)
                blockLogs[i].LogIndex = i;
        }

        private static int CompareByTransactionThenPositionWithinTransaction(FilteredLog a, FilteredLog b)
        {
            var byTransaction = a.TransactionIndex.CompareTo(b.TransactionIndex);
            return byTransaction != 0 ? byTransaction : a.LogIndex.CompareTo(b.LogIndex);
        }
    }
}
