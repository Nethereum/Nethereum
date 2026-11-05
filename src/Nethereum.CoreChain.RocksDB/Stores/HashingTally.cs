using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class HashingTally
    {
        private readonly FlatReconcileEnv _env;
        private readonly Action<string> _progress;
        private readonly Stopwatch _sw;
        private readonly object _gate = new object();
        private long _unitsDone, _rangesDone, _leaves, _lastReportedLeaves;
        private DateTimeOffset _lastReportedAt;

        public HashingTally(FlatReconcileEnv env, Action<string> progress, Stopwatch sw)
        {
            _env = env;
            _progress = progress;
            _sw = sw;
            _lastReportedAt = env.Clock.GetUtcNow();
        }

        public void Done(long leaves, bool isUnit) => Record(leaves, isUnit ? 1 : 0, isUnit ? 0 : 1);

        public void Walked(long leaves) => Record(leaves, 0, 0);

        private void Record(long leaves, int units, int ranges)
        {
            if (_progress == null) return;
            string line = null;
            lock (_gate)
            {
                _unitsDone += units;
                _rangesDone += ranges;
                _leaves += leaves;
                if (_env.IsProgressDue(_leaves, FlatReconcileEnv.ProgressSlotInterval, ref _lastReportedLeaves, ref _lastReportedAt))
                    line = $"certify.A units_done={_unitsDone}/{FlatUnitPass.UnitCount} large_owner_ranges_done={_rangesDone:N0} leaves={_leaves:N0} " +
                           $"leaves_per_s={_leaves / Math.Max(_sw.Elapsed.TotalSeconds, 0.001):N0} elapsed={_sw.Elapsed:hh\\:mm\\:ss}";
            }
            if (line != null) _progress(line);
        }

    internal static string ShardProgressLine(int shard, List<FlatUnit> units, TimeSpan elapsed)
    {
        long accounts = 0, slots = 0, missing = 0, differing = 0;
        foreach (var unit in units)
        {
            if (unit.Shard != shard) continue;
            accounts += unit.Accounts;
            slots += unit.Slots;
            missing += unit.TrieNodesMissing;
            differing += unit.TrieNodesDiffering;
        }
        return ShardProgressLine(shard, accounts, slots, missing, differing, elapsed);
    }

    internal static string ShardProgressLine(int shard, long accounts, long slots, long missing, long differing, TimeSpan elapsed)
        => $"certify.A shard={shard:X} accounts={accounts:N0} slots={slots:N0} " +
           $"leaves_per_s={(accounts + slots) / Math.Max(elapsed.TotalSeconds, 0.001):N0} " +
           $"trie_missing={missing:N0} trie_differ={differing:N0} elapsed={elapsed:hh\\:mm\\:ss}";
    }
}
