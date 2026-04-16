using System.Collections.Generic;

namespace Nethereum.EVM
{
    public interface IBlockRootCalculator
    {
        byte[] ComputeTransactionsRoot(List<byte[]> encodedTransactions);
        byte[] ComputeReceiptsRoot(List<byte[]> encodedReceipts);
    }
}
