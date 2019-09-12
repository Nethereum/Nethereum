using System;

namespace Nethereum.RPC.Eth.DTOs
{
    public static class BlockExtensions
    {
        /// <summary>
        /// If the Block type matches BlockWithTransactions or BlockWithTransactionHashes it returns the count (see SupportsTransactionCount).
        /// </summary>
        /// <param name="block">the block</param>
        /// <param name="throwWhenNotSupported">throw an exception when the type does not support a transaction count</param>
        /// <returns></returns>
        public static int TransactionCount(this Block block, bool throwWhenNotSupported = true)
        {
            if (block is BlockWithTransactions b)
                return b.Transactions?.Length ?? 0;

            if (block is BlockWithTransactionHashes bh)
                return bh.TransactionHashes?.Length ?? 0;

            if(throwWhenNotSupported)
                throw new ArgumentException($"TransactionCount error.  {block.GetType().Name} does not support returning a transaction count.");

            return 0;
        }

        public static bool SupportsTransactionCount<BlockType>(this BlockType block) where BlockType : Block
        {
            if (block is BlockWithTransactions)
                return true;

            if (block is BlockWithTransactionHashes)
                return true;

            return false;
        }
    }
}
