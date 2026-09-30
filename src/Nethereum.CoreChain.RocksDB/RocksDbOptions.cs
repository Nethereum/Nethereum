namespace Nethereum.CoreChain.RocksDB
{
    public class RocksDbStorageOptions
    {
        public string DatabasePath { get; set; } = "./chaindata";

        public long BlockCacheSize { get; set; } = 1024L * 1024 * 1024;

        public int MaxOpenFiles { get; set; } = 10000;

        public int MaxBackgroundCompactions { get; set; } = 8;
        public int MaxBackgroundFlushes { get; set; } = 2;

        public int MaxBackgroundJobs { get; set; } = 8;

        public int MaxSubcompactions { get; set; } = 4;

        public bool EnableStatistics { get; set; } = false;

        public long DbWriteBufferSize { get; set; } = 3L * 1024 * 1024 * 1024;

        public long MaxTotalWalSize { get; set; } = 512 * 1024 * 1024;

        public long BytesPerSync { get; set; } = 1 * 1024 * 1024;

        public StoragePreset Preset { get; set; } = StoragePreset.MainnetFull;

        public bool RateLimiterEnabled { get; set; } = false;
        public long RateLimiterBytesPerSecond { get; set; } = 128L * 1024 * 1024;

        public bool BufferTrieWrites { get; set; } = false;

        public bool PathKeyedState { get; set; } = false;

        public int TrieNodeHistoryBlocks { get; set; } = -1;

        public bool TrieNodeHistoryIndex { get; set; } = false;

        public bool SplitHistoryStore { get; set; } = false;

        public int HotWindowBlocks { get; set; } = 128;

        public bool PromotionEnabled { get; set; } = false;

        public bool EnableLogIndex { get; set; } = false;

        public bool UseFreezerHistory { get; set; } = false;

        public string FreezerHistoryDirectory { get; set; }

        public bool BackgroundFreezeIndexing { get; set; } = false;

        public int FreezerBackgroundDegreeOfParallelism { get; set; } = 0;

        public Nethereum.Freezer.FilterMaps.FilterMapsParams? FilterMapsIndexParams { get; set; }

        public long? FreezerMaxFileSizeBytes { get; set; }

        public long FreezerCommitCadenceBlocks { get; set; } = 32_768;

        public long FreezerBulkResidentCeilingBytes { get; set; } = History.BulkIndexIngestor.DefaultResidentCeilingBytes;

        public int FreezerBulkSortDegreeOfParallelism { get; set; } = 0;

        public bool SkipBootReconcile { get; set; } = false;

        public void Validate()
        {
            if (TrieNodeHistoryBlocks < -1)
                throw new System.InvalidOperationException(
                    $"TrieNodeHistoryBlocks={TrieNodeHistoryBlocks} is invalid: use -1 (off), 0 (full/never-prune) or N>0 (retain N blocks).");

            if (TrieNodeHistoryIndex && TrieNodeHistoryBlocks < 0)
                throw new System.InvalidOperationException(
                    "TrieNodeHistoryIndex requires TrieNodeHistoryBlocks >= 0 (there is no node history to index when node history is off).");

            if (TrieNodeHistoryBlocks >= 0 && !PathKeyedState)
                throw new System.InvalidOperationException(
                    "TrieNodeHistoryBlocks >= 0 requires PathKeyedState = true (node history journals path-keyed state-trie nodes; the hash store cannot journal them).");
        }

        public RocksDbStorageOptions Clone() => (RocksDbStorageOptions)MemberwiseClone();
    }
}
