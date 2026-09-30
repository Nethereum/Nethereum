using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class HotBlockPromotionData
    {
        public BlockHeader Header { get; set; }
        public byte[] BlockHash { get; set; }
        public BlockMeta Meta { get; set; }
        public List<ISignedTransaction> Transactions { get; set; }
        public List<ReceiptInfo> Receipts { get; set; }

        public byte[] BlockAccessList { get; set; }
    }
}
