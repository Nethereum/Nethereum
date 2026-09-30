using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public static class GethTraceDivergenceReporter
    {
        public static async Task ReportAsync(
            ITestOutputHelper output,
            GeneralStateTestRunner runner,
            string testFile,
            SingleTestResult failure,
            string fork)
        {
            try
            {
                output.WriteLine("  Running trace comparison with Geth...");

                var gethRunner = new GethEvmRunner();
                if (!gethRunner.IsAvailable)
                {
                    output.WriteLine($"    Trace comparison unavailable: {Trim(gethRunner.UnavailableReason, 300)}");
                    return;
                }

                var gethResult = await gethRunner.RunStateTestAsync(
                    testFile, failure.DataIndex, failure.GasIndex, failure.ValueIndex, fork, failure.TestName);

                if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
                {
                    output.WriteLine($"    Geth trace unavailable: {Trim(gethResult.Error, 300) ?? "no steps"}");
                    return;
                }

                var nethResult = await runner.RunTestWithExecutorAsync(
                    testFile, specificDataIndex: failure.DataIndex, captureTraces: true);

                var nethSingle = nethResult.Results.FirstOrDefault(x =>
                    x.TestName == failure.TestName &&
                    x.DataIndex == failure.DataIndex &&
                    x.GasIndex == failure.GasIndex &&
                    x.ValueIndex == failure.ValueIndex);

                if (nethSingle?.Traces == null || nethSingle.Traces.Count == 0)
                {
                    output.WriteLine("    Nethereum trace unavailable: no traces captured");
                    return;
                }

                var comparer = new TraceComparer();
                var comparison = comparer.Compare(gethResult.Steps, comparer.NormalizeNethTrace(nethSingle.Traces));

                if (!comparison.HasDivergence)
                {
                    output.WriteLine($"    Traces MATCH ({gethResult.Steps.Count} steps) - divergence is in final state calculation");
                    return;
                }

                var diverge = comparison.Steps[comparison.FirstDivergenceStep - 1];
                output.WriteLine($"  TRACE DIVERGENCE at step {comparison.FirstDivergenceStep}: {Trim(comparison.DivergenceReason, 120)}");
                output.WriteLine($"    Geth: PC={diverge.GethPC} Op={Trim(diverge.GethOp, 32)} Gas={diverge.GethGas} Cost={diverge.GethCost} Depth={diverge.GethDepth}");
                output.WriteLine($"    Neth: PC={diverge.NethPC} Op={Trim(diverge.NethOp, 32)} Gas={diverge.NethGas} Cost={diverge.NethCost} Depth={diverge.NethDepth}");

                var start = Math.Max(0, comparison.FirstDivergenceStep - 6);
                var end = Math.Min(comparison.Steps.Count, comparison.FirstDivergenceStep + 5);
                output.WriteLine($"  Context (steps {start + 1}..{end}):");
                for (var i = start; i < end; i++)
                {
                    var s = comparison.Steps[i];
                    var marker = s.Step == comparison.FirstDivergenceStep ? ">>>" : "   ";
                    output.WriteLine($"    {marker} Step {s.Step}: PC={s.GethPC}/{s.NethPC} Op={Trim(s.GethOp, 32)} Gas={s.GethGas}/{s.NethGas} Cost={s.GethCost}/{s.NethCost} {Trim(s.DivergenceType, 80)}");
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"    Trace comparison error: {Trim(ex.Message, 300)}");
            }
        }

        private static string Trim(string value, int max)
            => value == null || value.Length <= max ? value : value.Substring(0, max) + "…";
    }
}
