using System;

namespace Nethereum.RPC.Eth.DTOs
{
    public static class BlockExtensions
    {
        public static int TransactionCount(this BlockWithTransactions block)
        {
            return block.Transactions?.Length ?? 0;
        }

        public static int TransactionCount(this BlockWithTransactionHashes block)
        {
            return block.TransactionHashes?.Length ?? 0;
        }
    }
}
