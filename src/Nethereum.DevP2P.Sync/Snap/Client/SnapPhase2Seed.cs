using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Client
{
    internal static class SnapPhase2Seed
    {
        public static List<SnapSyncAccountTask> Build(SnapSyncState resumeFrom, int concurrency, Action<int, int> warn)
        {
            List<SnapSyncAccountTask> seedTasks;
            if (resumeFrom != null && resumeFrom.Tasks != null && resumeFrom.Tasks.Count > 0)
            {
                seedTasks = new List<SnapSyncAccountTask>(resumeFrom.Tasks);
                if (seedTasks.Count < concurrency)
                {
                    warn?.Invoke(seedTasks.Count, concurrency);
                }
            }
            else
            {
                seedTasks = new List<SnapSyncAccountTask>(concurrency);
                var ranges = SnapHashRanges.SplitHashRange(SnapHashRanges.MinHash(), SnapHashRanges.MaxHash(), concurrency);
                foreach (var range in ranges)
                {
                    seedTasks.Add(new SnapSyncAccountTask
                    {
                        Next = range.Start,
                        Last = range.End,
                        StorageCompleted = Array.Empty<byte[]>(),
                        SubTasks = new Dictionary<byte[], IReadOnlyList<SnapSyncStorageSubTask>>(),
                    });
                }
            }
            return seedTasks;
        }

        public static Action<int, int> UndersizedResumeWarning(ILogger logger) => (count, conc) =>
            logger.LogWarning(
                "snap.resume.degraded tasks={Count} expected_concurrency={Conc}",
                count, conc);
    }
}
