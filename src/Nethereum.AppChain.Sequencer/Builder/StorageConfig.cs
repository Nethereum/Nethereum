namespace Nethereum.AppChain.Sequencer.Builder
{
    public enum StorageType
    {
        InMemory,
        RocksDb
    }

    public class StorageConfig
    {
        public StorageType Type { get; set; } = StorageType.InMemory;
        public string? Path { get; set; }

        public static StorageConfig InMemory() => new() { Type = StorageType.InMemory };

        public static StorageConfig RocksDb(string path) => new()
        {
            Type = StorageType.RocksDb,
            Path = path
        };
    }
}
