using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.EEST.Conformance;
using Nethereum.EEST.Conformance.Diagnosis;
using Nethereum.Testing;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class StateCaseDiagnosis
    {
        private static int _diagnosed;

        public static readonly string DiagnosisDir = Path.Combine(
            Corpora.RepoRoot ?? Directory.GetCurrentDirectory(), ".testresults", "conformance", "diagnosis");

        public static async Task<string?> DiagnoseAsync(ConformanceCase conformanceCase, StateTestLoader.StateTest test, ConformanceCaseResult result)
        {
            if (!ConformanceSettings.Diagnose) return null;
            if (result.Success && string.IsNullOrEmpty(ConformanceSettings.Case)) return null;
            if (Interlocked.Increment(ref _diagnosed) > ConformanceSettings.DiagnoseMax) return null;

            var lines = await ReportAsync(conformanceCase, test, result);
            Directory.CreateDirectory(DiagnosisDir);
            var path = Path.Combine(DiagnosisDir, FileNameFor(conformanceCase.Id));
            await File.WriteAllLinesAsync(path, lines);
            return path;
        }

        private const int FullTraceTableMaxSteps = 20000;
        private const int TraceTableContext = 50;

        public static async Task<List<string>> ReportAsync(ConformanceCase conformanceCase, StateTestLoader.StateTest test, ConformanceCaseResult result)
        {
            var lines = new List<string>
            {
                $"case: {conformanceCase.Id}",
                result.Success ? "result: pass" : $"failure: [{result.Kind}] {result.Detail}"
            };

            try
            {
                var evmPath = TestEnvironment.Tool("Evm");
                var gethRunner = new GethEvmRunner(evmPath);
                if (!gethRunner.IsAvailable)
                {
                    lines.Add($"    Trace comparison unavailable: {TraceDivergenceReport.Trim(gethRunner.UnavailableReason, 300)}");
                    return lines;
                }

                var execution = await EestStateTestsDriver.Instance.ExecuteWithTraceAsync(
                    test, conformanceCase.Fork, conformanceCase.DataIndex, conformanceCase.GasIndex, conformanceCase.ValueIndex);
                if (execution.Traces == null || execution.Traces.Count == 0)
                {
                    lines.Add("    Nethereum trace unavailable: no traces captured");
                    return lines;
                }

                var gethResult = await gethRunner.RunStateTestAsync(
                    conformanceCase.File, conformanceCase.DataIndex, conformanceCase.GasIndex, conformanceCase.ValueIndex,
                    conformanceCase.Fork, conformanceCase.Name);
                if (!gethResult.Success || gethResult.Steps == null || gethResult.Steps.Count == 0)
                {
                    lines.Add($"    Geth trace unavailable: {TraceDivergenceReport.Trim(gethResult.Error, 300) ?? "no steps"}");
                    return lines;
                }

                var comparer = new TraceComparer();
                var comparison = comparer.Compare(gethResult.Steps, comparer.NormalizeNethTrace(execution.Traces));
                lines.AddRange(TraceDivergenceReport.Lines(comparison, gethResult.Steps.Count));

                if (execution.ExecutionState != null)
                    lines.AddRange(await PostStateLinesAsync(conformanceCase, execution, evmPath));

                lines.Add("");
                if (comparison.HasDivergence)
                    lines.Add(comparer.GenerateTraceTable(comparison, TraceTableContext));
                else if (comparison.Steps.Count <= FullTraceTableMaxSteps)
                    lines.Add(comparer.GenerateTraceTable(comparison));
                else
                    lines.Add($"trace table omitted: {comparison.Steps.Count} steps exceed {FullTraceTableMaxSteps}");
            }
            catch (Exception ex)
            {
                lines.Add($"    Trace comparison error: {TraceDivergenceReport.Trim(ex.Message, 300)}");
            }

            return lines;
        }

        private static async Task<IEnumerable<string>> PostStateLinesAsync(ConformanceCase conformanceCase, StateCaseExecution execution, string evmPath)
        {
            var t8nResult = await new GethT8nRunner(evmPath).RunT8nAsync(
                conformanceCase.File, conformanceCase.DataIndex, conformanceCase.GasIndex, conformanceCase.ValueIndex,
                conformanceCase.Fork, conformanceCase.Name);
            if (!t8nResult.Success || t8nResult.PostState == null)
                return new[] { $"  GETH T8N: FAILED - {TraceDivergenceReport.Trim(t8nResult.Error, 300)}" };

            var diff = new PostStateSignatureClassifier().Compare(
                execution.ExecutionState!.AccountsState.ToDictionary(kvp => kvp.Key.ToHexLower(), kvp => kvp.Value),
                t8nResult.PostState, execution.Coinbase, execution.Sender);

            var lines = new List<string>
            {
                $"  GETH t8n stateRoot: {t8nResult.StateRoot}",
                $"  DIFF Signature: {diff.Signature} ({diff.Diffs.Count} field diffs)"
            };
            lines.AddRange(diff.Diffs.Take(20).Select(d =>
                $"    {d.Address} {d.Field}: geth={TraceDivergenceReport.Trim(d.GethValue, 80)} neth={TraceDivergenceReport.Trim(d.NethValue, 80)}"));
            return lines;
        }

        private static string FileNameFor(string caseId)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '[', ']', ',', ' ' }).ToHashSet();
            var builder = new StringBuilder(caseId.Length);
            foreach (var ch in caseId) builder.Append(invalid.Contains(ch) ? '_' : ch);
            var name = builder.ToString();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(caseId)), 0, 6).ToLowerInvariant();
            if (name.Length > 150) name = name.Substring(name.Length - 150);
            return $"{name}-{hash}.txt";
        }
    }
}
