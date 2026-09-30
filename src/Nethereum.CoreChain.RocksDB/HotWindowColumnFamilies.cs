using System.Collections.Generic;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "HotWindowColumnFamilies - the tip-band column families of the hot-window/history split")]
    public static class HotWindowColumnFamilies
    {
        public static IReadOnlyList<(string Name, LiveCfProfile Profile)> Catalogue { get; } =
            new (string, LiveCfProfile)[]
            {
                (RocksDbManager.CF_HOT_BLOCK_HEADER, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_HOT_BLOCK_META, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_HOT_BLOCK_HASH_INDEX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_HOT_TX_BODY, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_HOT_TX_HASH_INDEX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_HOT_RECEIPT_BODY, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_HOT_BLOCK_ACCESS_LIST, LiveCfProfile.LocationSequential),
            };
    }
}
