using System.Collections.Generic;

using Nethereum.Documentation;
namespace Nethereum.CoreChain.Storage
{
    public sealed record SnapSyncState
    {
        public required ulong SchemaVersion { get; init; }

        public required SnapPhase Phase { get; init; }

        public required ulong PivotBlockNumber { get; init; }

        public required byte[] PivotBlockHash { get; init; }

        public required byte[] HealTargetRoot { get; init; }

        public required IReadOnlyList<SnapSyncAccountTask> Tasks { get; init; }

        public required SnapSyncCounters Counters { get; init; }
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "SnapPhase - the persisted snap-sync phase the follower fast-starts from")]
    public enum SnapPhase : byte
    {
        NotStarted    = 0,
        Phase2Running = 1,
        Phase3Running = 2,
        Complete      = 3,
        Generating    = 4,
    }

    public sealed record SnapSyncAccountTask
    {
        public required byte[] Next { get; init; }

        public required byte[] Last { get; init; }

        public required IReadOnlyList<byte[]> StorageCompleted { get; init; }

        public required IReadOnlyDictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>> SubTasks { get; init; }
    }

    public sealed record SnapSyncStorageSubTask
    {
        public required byte[] AccountHash { get; init; }

        public required byte[] Next { get; init; }

        public required byte[] Last { get; init; }

        public required byte[] StorageRoot { get; init; }
    }

    public sealed record SnapSyncCounters
    {
        public required ulong AccountsSynced { get; init; }
        public required ulong AccountBytes { get; init; }
        public required ulong StorageSlotsSynced { get; init; }
        public required ulong StorageBytes { get; init; }
        public required ulong BytecodesSynced { get; init; }
        public required ulong BytecodeBytes { get; init; }
        public required ulong TrieNodesHealed { get; init; }
        public required ulong TrieNodeBytesHealed { get; init; }
        public required ulong BytecodesHealed { get; init; }

        public static SnapSyncCounters Zero { get; } = new()
        {
            AccountsSynced = 0,
            AccountBytes = 0,
            StorageSlotsSynced = 0,
            StorageBytes = 0,
            BytecodesSynced = 0,
            BytecodeBytes = 0,
            TrieNodesHealed = 0,
            TrieNodeBytesHealed = 0,
            BytecodesHealed = 0,
        };
    }
}
