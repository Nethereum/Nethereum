using System.Collections.Generic;

namespace Nethereum.Model.SSZ
{
    /// <summary>
    /// EIP-7807 SSZ <see cref="IBlockRootsProvider"/>. Delegates to
    /// <see cref="SszRootCalculator"/> which computes each root as
    /// <c>hash_tree_root(ProgressiveList[T])</c>. Patricia tries are not
    /// touched on the SSZ path.
    /// </summary>
    public class SszBlockRootsProvider : IBlockRootsProvider
    {
        public static SszBlockRootsProvider Instance { get; } = new SszBlockRootsProvider();

        public byte[] CalculateTransactionsRoot(IList<ISignedTransaction> transactions)
            => SszRootCalculator.Current.CalculateTransactionsRoot(transactions);

        public byte[] CalculateReceiptsRoot(IList<Receipt> receipts)
            => SszRootCalculator.Current.CalculateReceiptsRoot(receipts);
    }
}
