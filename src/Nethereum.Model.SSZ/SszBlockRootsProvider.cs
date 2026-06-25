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

        public byte[] CalculateWithdrawalsRoot(IList<Withdrawal> withdrawals)
        {
            var tuples = new List<(ulong, ulong, byte[], ulong)>(withdrawals?.Count ?? 0);
            if (withdrawals != null)
            {
                foreach (var w in withdrawals)
                    tuples.Add((w.Index, w.ValidatorIndex, w.Address, w.AmountInGwei));
            }
            return SszRootCalculator.Current.CalculateWithdrawalsRoot(tuples);
        }
    }
}
