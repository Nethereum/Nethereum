using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage
{
    public sealed record SnapSyncState
    {
        /// <summary>Bumped on any breaking change to the persisted struct shape. Reader treats unknown versions as fresh state (NotStarted) and emits a WARN.</summary>
        public required ulong SchemaVersion { get; init; }

        public required SnapPhase Phase { get; init; }

        public required ulong PivotBlockNumber { get; init; }

        /// <summary>32 bytes. Pinned so a pivot rotation that happened pre-kill is preserved across restart.</summary>
        public required byte[] PivotBlockHash { get; init; }

        /// <summary>32 bytes. Set when transitioning into Phase3Running; zero / unused while in Phase2Running.</summary>
        public required byte[] HealTargetRoot { get; init; }

        public required IReadOnlyList<SnapSyncAccountTask> Tasks { get; init; }

        public required SnapSyncCounters Counters { get; init; }
    }

    public enum SnapPhase : byte
    {
        NotStarted    = 0,
        Phase2Running = 1,
        Phase3Running = 2,
        Complete      = 3,
    }

    public sealed record SnapSyncAccountTask
    {
        /// <summary>32-byte hash cursor — the next account-range request starts here.</summary>
        public required byte[] Next { get; init; }

        /// <summary>32-byte end-of-range (inclusive).</summary>
        public required byte[] Last { get; init; }

        /// <summary>Account hashes whose storage subtree has been fully fetched within this task. Lets resume skip re-walking storage for accounts already complete.</summary>
        public required IReadOnlyList<byte[]> StorageCompleted { get; init; }

        /// <summary>Per-large-contract storage fan-out. Keyed by 32-byte account hash; each value is one of the partition sub-ranges the storage trie is being streamed in.</summary>
        public required IReadOnlyDictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>> SubTasks { get; init; }
    }

    public sealed record SnapSyncStorageSubTask
    {
        public required byte[] AccountHash { get; init; }

        /// <summary>32-byte hash cursor — the next StorageRanges request starts here.</summary>
        public required byte[] Next { get; init; }

        /// <summary>32-byte end-of-sub-range (inclusive).</summary>
        public required byte[] Last { get; init; }

        /// <summary>Storage root we were chasing. If the live account's storage root no longer matches, the subtask is invalidated and the account is re-queued for heal.</summary>
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
