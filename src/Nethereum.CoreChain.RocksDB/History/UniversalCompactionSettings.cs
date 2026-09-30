namespace Nethereum.CoreChain.RocksDB.History
{
    public struct UniversalCompactionSettings
    {
        public int Level0FileNumCompactionTrigger;
        public int MinMergeWidth;
        public int MaxMergeWidth;
        public int SizeRatioPercent;
        public int MaxSizeAmplificationPercent;
        public bool StopStyleSimilarSize;
        public ulong WriteBufferSize;
        public ulong TargetFileSizeBase;

        public ulong PeriodicCompactionSeconds;

        public bool PartitionedFilters;

        public static UniversalCompactionSettings BulkDefault => new UniversalCompactionSettings
        {
            Level0FileNumCompactionTrigger = 4,
            MinMergeWidth = 4,
            MaxMergeWidth = 8,
            SizeRatioPercent = 1,
            MaxSizeAmplificationPercent = 1_000_000,
            StopStyleSimilarSize = true,
            WriteBufferSize = 256UL * 1024 * 1024,
            TargetFileSizeBase = 2048UL * 1024 * 1024,
            PeriodicCompactionSeconds = 0,
            PartitionedFilters = false,
        };

        public static UniversalCompactionSettings TrieDefault => new UniversalCompactionSettings
        {
            Level0FileNumCompactionTrigger = 4,
            MinMergeWidth = 4,
            MaxMergeWidth = 8,
            SizeRatioPercent = 1,
            MaxSizeAmplificationPercent = 200,
            StopStyleSimilarSize = true,
            WriteBufferSize = 128UL * 1024 * 1024,
            TargetFileSizeBase = 256UL * 1024 * 1024,
            PeriodicCompactionSeconds = 7UL * 24 * 60 * 60,
            PartitionedFilters = true,
        };
    }
}
