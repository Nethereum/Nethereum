using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.RocksDB
{
    public sealed partial class RocksDbChainStoreBundle
    {
        public ulong? PromotionFloor(ulong currentHead)
        {
            if (_promotionService == null || _promotionMaxHistoryBlocks == 0) return null;
            return currentHead > _promotionMaxHistoryBlocks ? currentHead - _promotionMaxHistoryBlocks : 0UL;
        }
    }
}
