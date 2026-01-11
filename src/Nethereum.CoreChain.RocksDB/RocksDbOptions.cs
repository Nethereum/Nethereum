namespace Nethereum.CoreChain.RocksDB
{
    public class RocksDbStorageOptions
    {
        public string DatabasePath { get; set; } = "./chaindata";
        public long BlockCacheSize { get; set; } = 128 * 1024 * 1024; // 128MB
        public int MaxBackgroundCompactions { get; set; } = 4;
        public int MaxBackgroundFlushes { get; set; } = 2;
        public int BloomFilterBitsPerKey { get; set; } = 10;
        public bool EnableStatistics { get; set; } = false;
        public long WriteBufferSize { get; set; } = 64 * 1024 * 1024; // 64MB
        public int MaxWriteBufferNumber { get; set; } = 3;
    }
}
