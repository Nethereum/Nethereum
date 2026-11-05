using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public sealed class SnapTaskFrontier : ISnapTaskFrontier
    {
        private readonly IReadOnlyList<SnapSyncAccountTask> _tasks;
        private readonly HashSet<string>[] _storageCompleted;
        private readonly Dictionary<string, IReadOnlyList<SnapSyncStorageSubTask>>[] _subTasks;

        public SnapTaskFrontier(IReadOnlyList<SnapSyncAccountTask> tasks)
        {
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _storageCompleted = new HashSet<string>[tasks.Count];
            _subTasks = new Dictionary<string, IReadOnlyList<SnapSyncStorageSubTask>>[tasks.Count];
            for (var i = 0; i < tasks.Count; i++)
            {
                var completed = new HashSet<string>();
                if (tasks[i].StorageCompleted != null)
                    foreach (var hash in tasks[i].StorageCompleted) completed.Add(hash.ToHex());
                _storageCompleted[i] = completed;

                var subs = new Dictionary<string, IReadOnlyList<SnapSyncStorageSubTask>>();
                if (tasks[i].SubTasks != null)
                    foreach (var entry in tasks[i].SubTasks) subs[entry.Key.ToHex()] = entry.Value;
                _subTasks[i] = subs;
            }
        }

        public bool IsAccountFetched(byte[] accountHash)
        {
            for (var i = 0; i < _tasks.Count; i++)
            {
                if (ByteArrayComparer.Current.Compare(accountHash, _tasks[i].Last) <= 0)
                    return ByteArrayComparer.Current.Compare(accountHash, _tasks[i].Next) < 0;
            }
            return true;
        }

        public bool IsStorageFetched(byte[] accountHash, byte[] slotHash)
        {
            for (var i = 0; i < _tasks.Count; i++)
            {
                if (ByteArrayComparer.Current.Compare(accountHash, _tasks[i].Last) > 0) continue;
                if (ByteArrayComparer.Current.Compare(accountHash, _tasks[i].Next) < 0) return true;
                if (_storageCompleted[i].Contains(accountHash.ToHex())) return true;
                if (!_subTasks[i].TryGetValue(accountHash.ToHex(), out var subtasks)) return false;

                foreach (var sub in subtasks)
                {
                    if (ByteArrayComparer.Current.Compare(slotHash, sub.Last) <= 0)
                        return ByteArrayComparer.Current.Compare(slotHash, sub.Next) < 0;
                }
                return true;
            }
            return true;
        }
    }
}
