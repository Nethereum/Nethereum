using System.Collections.Generic;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB
{
    public enum LiveCfProfile
    {
        HotTiny,
        RandomPoint,
        LocationSequential,
        SecondaryIndex,
        UniversalBulkPoint
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "StoragePreset - the per-column-family tuning presets")]
    public enum StoragePreset
    {
        MainnetFull,
        MainnetArchive,
        AppChainSmall,
        SyncBulk
    }

    public static class LiveColumnFamilies
    {
        public static IReadOnlyList<(string Name, LiveCfProfile Profile)> Catalogue { get; } =
            new (string, LiveCfProfile)[]
            {
                (RocksDbManager.CF_BLOCKS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_BLOCK_NUMBERS, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_TRANSACTIONS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_TX_BY_BLOCK, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_UNCLES, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_WITHDRAWALS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_RECEIPTS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_LOGS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_LOG_BY_BLOCK, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_LOG_BY_ADDRESS, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_STATE_ACCOUNTS, LiveCfProfile.RandomPoint),
                (RocksDbManager.CF_STATE_STORAGE, LiveCfProfile.RandomPoint),
                (RocksDbManager.CF_STATE_CODE, LiveCfProfile.RandomPoint),
                (RocksDbManager.CF_TRIE_NODES, LiveCfProfile.RandomPoint),
                (RocksDbManager.CF_STATE_TRIE_ACCOUNT, LiveCfProfile.UniversalBulkPoint),
                (RocksDbManager.CF_STATE_TRIE_STORAGE, LiveCfProfile.UniversalBulkPoint),
                (RocksDbManager.CF_NODE_HISTORY, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_NODE_HISTORY_INDEX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_STATE_ROOT_INDEX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_BINARY_TRIE_NODES, LiveCfProfile.RandomPoint),
                (RocksDbManager.CF_BINARY_TRIE_DEPTH_IDX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_BINARY_TRIE_ADDR_STEMS, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_FILTERS, LiveCfProfile.HotTiny),
                (RocksDbManager.CF_METADATA, LiveCfProfile.HotTiny),
                (RocksDbManager.CF_BLOCK_BLOOMS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_RECEIPT_BY_BLOCK, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_LOG_BY_TX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_MSG_RESULTS, LiveCfProfile.RandomPoint),
                (RocksDbManager.CF_MSG_RESULTS_BY_LEAF, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_STATE_HISTORY_ACCOUNTS, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_STATE_HISTORY_STORAGE, LiveCfProfile.LocationSequential),
                (RocksDbManager.CF_STATE_HISTORY_BLOCK_INDEX, LiveCfProfile.SecondaryIndex),
                (RocksDbManager.CF_STATE_HISTORY_META, LiveCfProfile.HotTiny)
            };
    }
}
