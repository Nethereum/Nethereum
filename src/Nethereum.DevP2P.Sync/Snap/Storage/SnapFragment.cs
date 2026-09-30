namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public abstract record SnapFragment
    {
        public sealed record AccountRange(int TaskIndex, byte[] Origin, byte[] Limit) : SnapFragment;

        public sealed record StorageSubtask(
            int TaskIndex, byte[] AccountHash, byte[] StorageRoot,
            int SubtaskIndex, byte[] Next, byte[] Last) : SnapFragment;

        public sealed record SmallStorageBatch(int TaskIndex, SmallBatchItem[] Items) : SnapFragment;

        public sealed record Bytecode(byte[][] Hashes) : SnapFragment;
    }

    public readonly record struct SmallBatchItem(byte[] AccountHash, byte[] StorageRoot);

    public readonly record struct AccountClassification(byte[] Hash, byte[] StorageRoot, byte[] CodeHash);

    public enum SmallStorageResult { Done, Large, Heal }

    public readonly record struct SmallStorageOutcome(byte[] Account, SmallStorageResult Result, byte[] ResumeFrom);
}
