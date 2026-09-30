using System.Collections.Generic;

namespace Nethereum.Model.SSZ
{
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
