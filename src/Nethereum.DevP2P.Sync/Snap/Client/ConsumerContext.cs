using System;
using System.Collections.Concurrent;
using Nethereum.Merkle.Patricia.Storage;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class ConsumerContext
    {
        public SnapSyncClient Client { get; init; }
        public SnapTaskSet TaskSet { get; init; }
        public byte[] AttemptRoot { get; init; }
        public FrozenRoot Frozen { get; init; }
        public ConcurrentDictionary<int, ActiveSnapLeaseInfo> Leases { get; init; }
        public ITrieNodeStore StorageNodeStore { get; init; }
        public SnapPhase2State State { get; init; }
        public SnapPhase2Checkpointer Checkpointer { get; init; }
        public Action AddAccount { get; init; }
    }
}
