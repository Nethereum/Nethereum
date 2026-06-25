using System.Collections.Generic;

namespace Nethereum.Model
{
    /// <summary>
    /// Strategy for computing the per-block roots that land in the block
    /// header (<c>transactionsRoot</c>, <c>receiptsRoot</c>). Patricia-trie
    /// chains compute an RLP + MPT root; EIP-7807 SSZ chains compute
    /// <c>hash_tree_root(ProgressiveList[T])</c> and skip the Patricia path
    /// entirely. An AppChain picks one at genesis via
    /// <see cref="Nethereum.AppChain.AppChainFork"/>.
    ///
    /// This is distinct from <c>Nethereum.EVM.IBlockRootCalculator</c>, which
    /// takes pre-encoded byte arrays and is used during block execution /
    /// replay (where encoding happens upstream). Here we take typed objects
    /// because SSZ needs them to compute <c>hash_tree_root</c> per EIP-6404 /
    /// EIP-6466, and the typed-object surface lets the Patricia side encode
    /// via the injected <see cref="IBlockEncodingProvider"/>.
    /// </summary>
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
