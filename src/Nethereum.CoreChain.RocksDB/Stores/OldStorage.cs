namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal readonly record struct OldStorage(bool Known, byte[] StorageRoot);
}
