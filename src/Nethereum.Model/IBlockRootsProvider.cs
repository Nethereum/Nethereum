using System.Collections.Generic;

namespace Nethereum.Model
{
    public interface IBlockRootsProvider
    {
        byte[] CalculateTransactionsRoot(IList<ISignedTransaction> transactions);
        byte[] CalculateReceiptsRoot(IList<Receipt> receipts);

        /// <summary>
        /// Compute the Shanghai+ <c>withdrawalsRoot</c> from a body's
        /// withdrawals list. Implementations either build a Patricia trie over
        /// <c>RLP(index) → RLP(withdrawal)</c> entries (mainnet) or compute
        /// <c>hash_tree_root(ProgressiveList[Withdrawal])</c> (EIP-7807 SSZ).
        /// A null or empty list resolves to the canonical empty-trie root.
        /// </summary>
        byte[] CalculateWithdrawalsRoot(IList<Withdrawal> withdrawals);
    }
}
