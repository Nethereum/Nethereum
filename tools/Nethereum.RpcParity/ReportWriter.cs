using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.RpcParity
{
    /// <summary>
    /// Streams one console line per completed case and accumulates per-group
    /// pass/diff/error counts for the final summary.
    /// </summary>
    public sealed class ReportWriter
    {
        private const int MaxValueLength = 120;

        private readonly Dictionary<string, (int Match, int Diff, int Error)> _byGroup =
            new Dictionary<string, (int Match, int Diff, int Error)>(StringComparer.OrdinalIgnoreCase);

        public void Record(CaseResult result)
        {
            var counts = _byGroup.TryGetValue(result.Group, out var existing) ? existing : (Match: 0, Diff: 0, Error: 0);

            switch (result.Verdict)
            {
                case Verdict.Match:
                    var suffix = result.Note == null ? "" : $" [{result.Note}]";
                    Console.WriteLine($"[{result.Group}] MATCH {result.Method}({result.Input}){suffix}");
                    counts.Match++;
                    break;
                case Verdict.Diff:
                    foreach (var diff in result.Diffs)
                        Console.WriteLine($"[{result.Group}] DIFF {result.Method}({result.Input}) at {diff.Path}: X={Truncate(diff.XValue)} Y={Truncate(diff.YValue)}");
                    counts.Diff++;
                    break;
                case Verdict.Error:
                    Console.WriteLine($"[{result.Group}] ERROR {result.Method}({result.Input}) - {result.ErrorMessage}");
                    counts.Error++;
                    break;
            }

            _byGroup[result.Group] = counts;
        }

        /// <summary>Prints the per-group and overall summary; returns the overall diff count.</summary>
        public int PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("=== summary ===");
            int totalPass = 0, totalDiff = 0, totalError = 0;
            foreach (var group in _byGroup.Keys.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
            {
                var (match, diff, error) = _byGroup[group];
                Console.WriteLine($"  {group,-10} pass={match} diff={diff} error={error}");
                totalPass += match;
                totalDiff += diff;
                totalError += error;
            }

            Console.WriteLine();
            Console.WriteLine($"OVERALL pass={totalPass} diff={totalDiff} error={totalError}");
            return totalDiff;
        }

        private static string Truncate(object value)
        {
            var s = value?.ToString() ?? "null";
            return s.Length <= MaxValueLength ? s : s.Substring(0, MaxValueLength) + "...";
        }
    }
}
