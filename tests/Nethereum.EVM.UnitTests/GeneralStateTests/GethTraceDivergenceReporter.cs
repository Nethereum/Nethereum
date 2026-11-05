using Nethereum.EEST.Conformance.Diagnosis;
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

                var gethRunner = GethTools.EvmRunner();
                if (!gethRunner.IsAvailable)
                {
                    output.WriteLine($"    Trace comparison unavailable: {TraceDivergenceReport.Trim(gethRunner.UnavailableReason, 300)}");
                    return;
                }

                var gethResult = await gethRunner.RunStateTestAsync(
                    testFile, failure.DataIndex, failure.GasIndex, failure.ValueIndex, fork, failure.TestName);

                if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
                {
                    output.WriteLine($"    Geth trace unavailable: {TraceDivergenceReport.Trim(gethResult.Error, 300) ?? "no steps"}");
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
                foreach (var line in TraceDivergenceReport.Lines(comparison, gethResult.Steps.Count))
                    output.WriteLine(line);
            }
            catch (Exception ex)
            {
                output.WriteLine($"    Trace comparison error: {TraceDivergenceReport.Trim(ex.Message, 300)}");
            }
        }
    }
}
