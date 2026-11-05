using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class FlatUnitPass
    {
        internal const long LargeOwnerSlotThreshold = 65_536;
        internal const int UnitsPerShard = 16;
        internal const int UnitCount = FlatStateWalk.ShardCount * UnitsPerShard;

        private readonly FlatReconcileEnv _env;
        private readonly UnitJudge _judge;

        public FlatUnitPass(FlatReconcileEnv env, UnitJudge judge)
        {
            _env = env;
            _judge = judge;
        }

        public async Task<List<FlatUnit>> RunAsync(
            byte[] root, bool dryRun, bool skipUnitsMarkedDone, Action<string> progress, Stopwatch sw, CancellationToken ct)
        {
            using var queue = new WorkQueue(ct);
            var tally = new HashingTally(_env, progress, sw);
            var ctx = new UnitContext(_env, root, queue, tally, progress, dryRun, skipUnitsMarkedDone);
            var units = new List<FlatUnit>();
            for (int i = 0; i < UnitCount; i++)
            {
                if (skipUnitsMarkedDone && CertifyJournal.IsUnitDone(_env.Rocks, i)) continue;
                var unit = new FlatUnit(ctx, i, HashLargeOwnerRange);
                units.Add(unit);
                queue.Enqueue(() => HashUnit(unit));
            }
            var workerCount = Math.Min(Environment.ProcessorCount, UnitCount);
            progress?.Invoke($"certify.queue units={units.Count} workers={workerCount}");
            await queue.DrainAsync(workerCount).ConfigureAwait(false);
            if (progress != null && units.Count > 0)
                for (int shard = 0; shard < FlatStateWalk.ShardCount; shard++) progress(HashingTally.ShardProgressLine(shard, units, sw.Elapsed));
            return units;
        }

        private void HashUnit(FlatUnit unit)
        {
            unit.Tally.Done(unit.Run(), isUnit: true);
            if (unit.Arrive()) unit.Queue.Enqueue(() => _judge.Judge(unit));
        }

        private void HashLargeOwnerRange(FlatUnit unit, NibbleRange range)
        {
            unit.Tally.Done(range.Hash(_env, unit.Queue.Token), isUnit: false);
            if (unit.Arrive()) unit.Queue.Enqueue(() => _judge.Judge(unit));
        }
    }
}
