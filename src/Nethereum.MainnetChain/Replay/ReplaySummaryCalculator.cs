using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.MainnetChain.Replay
{
    public static class ReplaySummaryCalculator
    {
        public static ReplaySummary Summarise(IReadOnlyList<ReplayBlockRecord> blocks)
        {
            var summary = new ReplaySummary();
            if (blocks.Count == 0) return summary;

            summary.WallMs = Stage(blocks.Select(b => b.WallMs));
            summary.ExecMs = Stage(blocks.Select(b => (double)b.ExecMs));
            summary.RootMs = Stage(blocks.Select(b => (double)b.RootMs));
            summary.PersistMs = Stage(blocks.Select(b => (double)b.PersistMs));
            summary.ExecutionMs = Stage(blocks.Select(b => b.ExecutionMs));
            summary.StateReadMs = Stage(blocks.Select(b => b.StateReadMs));
            summary.StateHashMs = Stage(blocks.Select(b => b.StateHashMs));
            summary.CommitMs = Stage(blocks.Select(b => b.CommitMs));
            summary.MedianAllocBytesPerBlock = Median(blocks.Select(b => (double)b.AllocBytes));
            summary.WindowPositions = blocks
                .GroupBy(b => b.WindowPosition)
                .OrderBy(g => g.Key)
                .Select(g => new ReplayWindowPositionStats
                {
                    Position = g.Key,
                    Blocks = g.Count(),
                    MedianWallMs = Median(g.Select(b => b.WallMs)),
                    MedianAllocBytes = Median(g.Select(b => (double)b.AllocBytes)),
                })
                .ToList();
            summary.Rss = new ReplayRssStats
            {
                First = blocks[0].RssBytes,
                Last = blocks[blocks.Count - 1].RssBytes,
                SlopeBytesPerBlock = Slope(blocks.Select(b => (double)b.RssBytes).ToList()),
            };
            summary.TotalGas = blocks.Sum(b => b.GasUsed);
            summary.TotalWallSeconds = blocks.Sum(b => b.WallMs) / 1000.0;
            summary.MegagasPerSecond = summary.TotalWallSeconds > 0
                ? summary.TotalGas / 1_000_000.0 / summary.TotalWallSeconds
                : 0;
            return summary;
        }

        public static double Median(IEnumerable<double> values) => Percentile(values, 0.5);

        public static double Percentile(IEnumerable<double> values, double fraction)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            if (sorted.Length == 0) return 0;
            var rank = (int)Math.Ceiling(fraction * sorted.Length);
            return sorted[Math.Max(0, rank - 1)];
        }

        private static ReplayStageStats Stage(IEnumerable<double> values)
        {
            var materialised = values.ToArray();
            return new ReplayStageStats
            {
                Median = Median(materialised),
                P90 = Percentile(materialised, 0.9),
            };
        }

        private static double Slope(IReadOnlyList<double> ys)
        {
            var n = ys.Count;
            if (n < 2) return 0;
            var meanX = (n - 1) / 2.0;
            var meanY = ys.Average();
            double numerator = 0, denominator = 0;
            for (var i = 0; i < n; i++)
            {
                numerator += (i - meanX) * (ys[i] - meanY);
                denominator += (i - meanX) * (i - meanX);
            }
            return numerator / denominator;
        }
    }
}
