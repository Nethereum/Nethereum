using System.Collections.Generic;
using System.Linq;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.Storage
{
    public sealed class SnapAccountTask
    {
        public byte[] Next;
        public byte[] Last;
        public bool RangeInFlight;
        public bool RangeDone;

        public byte[] DurableNext;

        public readonly Dictionary<byte[], byte[]> StateTasks = new(ByteArrayComparer.Current);

        public readonly Dictionary<byte[], LargeContractStorage> LargeContracts = new(ByteArrayComparer.Current);

        public readonly HashSet<byte[]> StorageCompleted = new(ByteArrayComparer.Current);
        public readonly HashSet<byte[]> DurableStorageCompleted = new(ByteArrayComparer.Current);

        public readonly List<byte[]> PendingStorageCompletedPromotion = new();

        public bool HasPendingStorage =>
            StateTasks.Count > 0 || LargeContracts.Values.Any(c => c.Pending > 0);

        public bool Done => RangeDone && !HasPendingStorage;
    }

    public sealed class LargeContractStorage
    {
        public byte[] AccountHash;
        public byte[] StorageRoot;
        public byte[] LastReprovedStateRoot;
        public bool ReproveInFlight;
        public object Scope;
        public readonly List<Subtask> Subtasks = new();
        public int Pending;
    }

    public sealed class Subtask
    {
        public byte[] Next;
        public byte[] Last;
        public bool InFlight;
        public bool Done;

        public byte[] DurableNext;
        public bool DurableDone;
    }
}
