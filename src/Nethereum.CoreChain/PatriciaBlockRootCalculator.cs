using System.Collections.Generic;
using Nethereum.EVM;
using Nethereum.RLP;

namespace Nethereum.CoreChain
{
    public class PatriciaBlockRootCalculator : IBlockRootCalculator
    {
        public byte[] ComputeTransactionsRoot(List<byte[]> encodedTransactions)
        {
            return ComputeOrderedRoot(encodedTransactions);
        }

        public byte[] ComputeReceiptsRoot(List<byte[]> encodedReceipts)
        {
            return ComputeOrderedRoot(encodedReceipts);
        }

        private byte[] ComputeOrderedRoot(List<byte[]> encodedItems)
        {
            var builder = new PatriciaMerkleTreeBuilder();
            for (int i = 0; i < encodedItems.Count; i++)
            {
                var key = RLP.RLP.EncodeElement(i.ToBytesForRLPEncoding());
                builder.Put(key, encodedItems[i]);
            }
            return builder.ComputeRoot();
        }
    }
}
