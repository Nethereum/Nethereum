namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal readonly record struct DiffSides(MissingNodeRecordingStore Old, MissingNodeRecordingStore New, byte[] OldRoot);
}
