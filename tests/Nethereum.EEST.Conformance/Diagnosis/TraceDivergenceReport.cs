using System;
using System.Collections.Generic;

namespace Nethereum.EEST.Conformance.Diagnosis
{
    public static class TraceDivergenceReport
    {
        public static IEnumerable<string> Lines(ComparisonResult comparison, int gethStepCount)
        {
            if (!comparison.HasDivergence)
            {
                yield return $"    Traces MATCH ({gethStepCount} steps) - divergence is in final state calculation";
                yield break;
            }

            var diverge = comparison.Steps[comparison.FirstDivergenceStep - 1];
            yield return $"  TRACE DIVERGENCE at step {comparison.FirstDivergenceStep}: {Trim(comparison.DivergenceReason, 120)}";
            yield return $"    Geth: PC={diverge.GethPC} Op={Trim(diverge.GethOp, 32)} Gas={diverge.GethGas} Cost={diverge.GethCost} Depth={diverge.GethDepth}";
            yield return $"    Neth: PC={diverge.NethPC} Op={Trim(diverge.NethOp, 32)} Gas={diverge.NethGas} Cost={diverge.NethCost} Depth={diverge.NethDepth}";

            var start = Math.Max(0, comparison.FirstDivergenceStep - 6);
            var end = Math.Min(comparison.Steps.Count, comparison.FirstDivergenceStep + 5);
            yield return $"  Context (steps {start + 1}..{end}):";
            for (var i = start; i < end; i++)
            {
                var s = comparison.Steps[i];
                var marker = s.Step == comparison.FirstDivergenceStep ? ">>>" : "   ";
                yield return $"    {marker} Step {s.Step}: PC={s.GethPC}/{s.NethPC} Op={Trim(s.GethOp, 32)} Gas={s.GethGas}/{s.NethGas} Cost={s.GethCost}/{s.NethCost} {Trim(s.DivergenceType, 80)}";
            }
        }

        public static string Trim(string value, int max)
            => value == null || value.Length <= max ? value : value.Substring(0, max) + "…";
    }
}
