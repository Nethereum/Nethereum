using System;
using Nethereum.Merkle.Patricia.Storage;
using static Nethereum.DevP2P.Sync.Snap.Client.SnapSyncClient;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal sealed class StorageFetchContext
    {
        public AccountWorkerResult Page { get; init; }
        public SnapTaskSet TaskSet { get; init; }
        public int TaskIndex { get; init; }
        public ITrieNodeStore StorageNodeStore { get; init; }
        public Func<byte[]> GetLiveRoot { get; init; }
        public FrozenRoot Frozen { get; init; }
        public Action MarkProductive { get; init; }
        public Action MarkFailure { get; init; }
        public ulong[] ReqIds { get; init; }

        internal static StorageFetchContext Create(
            ConsumerContext ctx, ConsumerState state, AccountWorkerResult page, int taskIndex,
            Action markProductive, Action markFailure)
            => new StorageFetchContext
            {
                Page = page,
                TaskSet = ctx.TaskSet,
                TaskIndex = taskIndex,
                StorageNodeStore = ctx.StorageNodeStore,
                GetLiveRoot = () => ctx.AttemptRoot,
                Frozen = ctx.Frozen,
                MarkProductive = markProductive,
                MarkFailure = markFailure,
                ReqIds = new ulong[] { state.ReqId },
            };
    }
}
