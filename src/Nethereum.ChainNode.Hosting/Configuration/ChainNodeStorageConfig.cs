namespace Nethereum.ChainNode.Hosting.Configuration
{
    public sealed class ChainNodeStorageConfig
    {
        public string DataDirectory { get; set; } = "./chain-data";

        public bool InMemory { get; set; }

        public bool PathKeyedState { get; set; }

        public int JournalBlocks { get; set; } = 128;

        public int TrieNodeHistoryBlocks { get; set; } = -1;

        public bool TrieNodeHistoryIndex { get; set; }

        public bool SplitHistoryStore { get; set; }

        public int HotWindowBlocks { get; set; } = 128;

        public bool PromotionEnabled { get; set; }

        public bool UseFreezerHistory { get; set; }

        public string? FreezerHistoryDirectory { get; set; }

        public bool BackgroundFreezeIndexing { get; set; }

        public int FreezerBackgroundDegreeOfParallelism { get; set; } = 0;

        public long BlockCacheSize { get; set; } = 1024L * 1024 * 1024;

        public int FlushCadenceBlocks { get; set; } = 1;

        public bool EnableLogIndex { get; set; }
    }
}
