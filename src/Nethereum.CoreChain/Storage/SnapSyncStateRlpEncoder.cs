using System.Collections.Generic;
using System.Numerics;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage
{
    public sealed class SnapSyncStateRlpEncoder : ISnapSyncStateEncoder
    {
        public const ulong CurrentSchemaVersion = 1;

        public static SnapSyncStateRlpEncoder Instance { get; } = new();

        public byte[] Encode(SnapSyncState state)
        {
            var tasks = new byte[state.Tasks.Count][];
            for (int i = 0; i < state.Tasks.Count; i++)
                tasks[i] = EncodeAccountTask(state.Tasks[i]);

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((BigInteger)state.SchemaVersion).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(new[] { (byte)state.Phase }),
                RLP.RLP.EncodeElement(((BigInteger)state.PivotBlockNumber).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(state.PivotBlockHash ?? new byte[32]),
                RLP.RLP.EncodeElement(state.HealTargetRoot ?? new byte[32]),
                RLP.RLP.EncodeList(tasks),
                EncodeCounters(state.Counters)
            );
        }

        public SnapSyncState Decode(byte[] data)
        {
            var top = (RLPCollection)RLP.RLP.Decode(data);
            var schemaVersion = top[0].RLPData.ToBigIntegerFromRLPDecoded();
            if ((ulong)schemaVersion != CurrentSchemaVersion) return null;

            var phaseBytes = top[1].RLPData;
            var phase = phaseBytes == null || phaseBytes.Length == 0
                ? SnapPhase.NotStarted
                : (SnapPhase)phaseBytes[0];
            var pivotBlockNumber = (ulong)top[2].RLPData.ToBigIntegerFromRLPDecoded();
            var pivotBlockHash = top[3].RLPData ?? new byte[32];
            var healTargetRoot = top[4].RLPData ?? new byte[32];
            var tasksList = (RLPCollection)top[5];
            var tasks = new List<SnapSyncAccountTask>(tasksList.Count);
            for (int i = 0; i < tasksList.Count; i++)
                tasks.Add(DecodeAccountTask((RLPCollection)tasksList[i]));
            var counters = DecodeCounters((RLPCollection)top[6]);

            return new SnapSyncState
            {
                SchemaVersion = CurrentSchemaVersion,
                Phase = phase,
                PivotBlockNumber = pivotBlockNumber,
                PivotBlockHash = pivotBlockHash,
                HealTargetRoot = healTargetRoot,
                Tasks = tasks,
                Counters = counters,
            };
        }

        private static byte[] EncodeAccountTask(SnapSyncAccountTask task)
        {
            var completed = new byte[task.StorageCompleted.Count][];
            for (int i = 0; i < task.StorageCompleted.Count; i++)
                completed[i] = RLP.RLP.EncodeElement(task.StorageCompleted[i] ?? new byte[32]);

            var subTaskEntries = new List<byte[]>(task.SubTasks.Count);
            foreach (var kv in task.SubTasks)
            {
                var inner = new byte[kv.Value.Count][];
                for (int i = 0; i < kv.Value.Count; i++)
                    inner[i] = EncodeStorageSubTask(kv.Value[i]);

                subTaskEntries.Add(RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(kv.Key ?? new byte[32]),
                    RLP.RLP.EncodeList(inner)
                ));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(task.Next ?? new byte[32]),
                RLP.RLP.EncodeElement(task.Last ?? new byte[32]),
                RLP.RLP.EncodeList(completed),
                RLP.RLP.EncodeList(subTaskEntries.ToArray())
            );
        }

        private static SnapSyncAccountTask DecodeAccountTask(RLPCollection task)
        {
            var next = task[0].RLPData ?? new byte[32];
            var last = task[1].RLPData ?? new byte[32];
            var completedList = (RLPCollection)task[2];
            var completed = new List<byte[]>(completedList.Count);
            for (int i = 0; i < completedList.Count; i++)
                completed.Add(completedList[i].RLPData ?? new byte[32]);

            var subList = (RLPCollection)task[3];
            var subDict = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(
                subList.Count, ByteArrayComparer.Current);
            for (int i = 0; i < subList.Count; i++)
            {
                var entry = (RLPCollection)subList[i];
                var key = entry[0].RLPData ?? new byte[32];
                var innerList = (RLPCollection)entry[1];
                var inner = new List<SnapSyncStorageSubTask>(innerList.Count);
                for (int j = 0; j < innerList.Count; j++)
                    inner.Add(DecodeStorageSubTask((RLPCollection)innerList[j]));
                subDict[key] = inner;
            }

            return new SnapSyncAccountTask
            {
                Next = next,
                Last = last,
                StorageCompleted = completed,
                SubTasks = subDict,
            };
        }

        private static byte[] EncodeStorageSubTask(SnapSyncStorageSubTask sub) =>
            RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(sub.AccountHash ?? new byte[32]),
                RLP.RLP.EncodeElement(sub.Next ?? new byte[32]),
                RLP.RLP.EncodeElement(sub.Last ?? new byte[32]),
                RLP.RLP.EncodeElement(sub.StorageRoot ?? new byte[32])
            );

        private static SnapSyncStorageSubTask DecodeStorageSubTask(RLPCollection sub) =>
            new SnapSyncStorageSubTask
            {
                AccountHash = sub[0].RLPData ?? new byte[32],
                Next = sub[1].RLPData ?? new byte[32],
                Last = sub[2].RLPData ?? new byte[32],
                StorageRoot = sub[3].RLPData ?? new byte[32],
            };

        private static byte[] EncodeCounters(SnapSyncCounters c) =>
            RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((BigInteger)c.AccountsSynced).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.AccountBytes).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.StorageSlotsSynced).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.StorageBytes).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.BytecodesSynced).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.BytecodeBytes).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.TrieNodesHealed).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.TrieNodeBytesHealed).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(((BigInteger)c.BytecodesHealed).ToBytesForRLPEncoding())
            );

        private static SnapSyncCounters DecodeCounters(RLPCollection c) =>
            new SnapSyncCounters
            {
                AccountsSynced = (ulong)c[0].RLPData.ToBigIntegerFromRLPDecoded(),
                AccountBytes = (ulong)c[1].RLPData.ToBigIntegerFromRLPDecoded(),
                StorageSlotsSynced = (ulong)c[2].RLPData.ToBigIntegerFromRLPDecoded(),
                StorageBytes = (ulong)c[3].RLPData.ToBigIntegerFromRLPDecoded(),
                BytecodesSynced = (ulong)c[4].RLPData.ToBigIntegerFromRLPDecoded(),
                BytecodeBytes = (ulong)c[5].RLPData.ToBigIntegerFromRLPDecoded(),
                TrieNodesHealed = (ulong)c[6].RLPData.ToBigIntegerFromRLPDecoded(),
                TrieNodeBytesHealed = (ulong)c[7].RLPData.ToBigIntegerFromRLPDecoded(),
                BytecodesHealed = (ulong)c[8].RLPData.ToBigIntegerFromRLPDecoded(),
            };
    }
}
