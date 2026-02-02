namespace Nethereum.AccountAbstraction.Bundler.RocksDB
{
    public class BundlerRocksDbOptions
    {
        public string DatabasePath { get; set; } = "./bundlerdata";
        public long BlockCacheSize { get; set; } = 64 * 1024 * 1024; // 64MB
        public int MaxBackgroundCompactions { get; set; } = 2;
        public int MaxBackgroundFlushes { get; set; } = 1;
        public int BloomFilterBitsPerKey { get; set; } = 10;
        public bool EnableStatistics { get; set; } = false;
        public long WriteBufferSize { get; set; } = 32 * 1024 * 1024; // 32MB
        public int MaxWriteBufferNumber { get; set; } = 2;
        public int MaxMempoolSize { get; set; } = 10000;
        public TimeSpan EntryTtl { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan IncludedEntryRetention { get; set; } = TimeSpan.FromMinutes(5);
    }
}
