using System.Collections.Generic;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.RocksDB.History
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain-rocksdb", "HistoryColumnFamilies - the history-side column families of the split store")]
    public static class HistoryColumnFamilies
    {
        public const string TxBody = "tx_body";
        public const string ReceiptBody = "receipt_body";
        public const string BlockHeader = "block_header";
        public const string BlockMeta = "block_meta";
        public const string BlockAccessList = "block_access_list";

        public const string TxHashIndex = "tx_hash_index";
        public const string BlockHashIndex = "block_hash_index";

        public const string LogBody = "log_body";
        public const string LogAddressIndex = "log_address_index";
        public const string LogTopicIndex = "log_topic_index";

        public const string LogFilterMaps = "log_filter_maps";

        public const string ForkHeaders = "fork_headers";

        public const string Control = "control";

        public enum HistoryCfProfile { Bulk, Control, LiveIndex, UniversalIndex }

        public static readonly IReadOnlyList<(string Name, HistoryCfProfile Profile)> Catalogue =
            new (string, HistoryCfProfile)[]
            {
                (TxBody,          HistoryCfProfile.Bulk),
                (ReceiptBody,     HistoryCfProfile.Bulk),
                (BlockHeader,     HistoryCfProfile.Bulk),
                (BlockMeta,       HistoryCfProfile.Bulk),
                (BlockAccessList, HistoryCfProfile.Bulk),
                (TxHashIndex,     HistoryCfProfile.Bulk),
                (BlockHashIndex,  HistoryCfProfile.Bulk),
                (LogBody,         HistoryCfProfile.Bulk),
                (LogAddressIndex, HistoryCfProfile.Bulk),
                (LogTopicIndex,   HistoryCfProfile.Bulk),
                (LogFilterMaps,   HistoryCfProfile.Bulk),
                (ForkHeaders,     HistoryCfProfile.Control),
                (Control,         HistoryCfProfile.Control),
            };

        /// <summary>
        /// Freezer-history-backend plan, Task 1: the narrow index-only slice of the catalogue that backs the
        /// freezer's own history database (<see cref="RocksDB.CatalogueScope.FreezerHistory"/>) — by-hash
        /// reverse lookups (<see cref="BlockHashIndex"/>/<see cref="TxHashIndex"/>) plus the EIP-7745 log index
        /// (<see cref="LogFilterMaps"/>). Deliberately NOT the full 13-CF <see cref="Catalogue"/> above: the
        /// freezer files themselves hold the bodies/receipts/headers, so this database only ever needs to
        /// answer "which item number is this hash" and "which epochs match this log filter".
        /// </summary>
        public static readonly IReadOnlyList<(string Name, HistoryCfProfile Profile)> FreezerHistoryCatalogue =
            new (string, HistoryCfProfile)[]
            {
                (BlockHashIndex, HistoryCfProfile.UniversalIndex),
                (TxHashIndex,    HistoryCfProfile.UniversalIndex),
                (LogFilterMaps,  HistoryCfProfile.LiveIndex),
                (Control,        HistoryCfProfile.Control),
            };
    }
}
