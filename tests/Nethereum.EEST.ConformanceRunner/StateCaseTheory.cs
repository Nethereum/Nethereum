using System;
using System.Threading.Tasks;
using Nethereum.EEST.Conformance;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class StateCaseTheory
    {
        public static async Task RunAsync(ICorpusSource source, string corpusName, string settingsKey, string caseId, HiveResultsSink sink)
        {
            if (caseId == Corpora.FilteredOutSentinel) return;

            if (caseId == Corpora.MissingCacheSentinel)
            {
                Assert.Fail(
                    $"{corpusName} not found at '{source.Root}'. Set ExternalPaths:{settingsKey} and follow external/README.md " +
                    "(dotnet run ci/fetch-externals.cs) before running this gate.");
                return;
            }

            if (caseId == Corpora.NoFixturesSentinel)
            {
                Assert.Fail(
                    $"{corpusName} at '{source.Root}' exists but contains zero state test " +
                    "*.json files - a category discovering 0 files must fail loudly, never be skipped.");
                return;
            }

            var conformanceCase = source.Find(caseId);
            Assert.True(conformanceCase != null, $"Case '{caseId}' is not in the corpus at '{source.Root}'.");

            var start = DateTimeOffset.UtcNow;
            var caseName = conformanceCase.DisplayName;
            var hiveCaseId = Corpora.HiveCaseId(conformanceCase);
            var testName = conformanceCase.Name;
            var file = conformanceCase.File;

            var tests = await StateTestFileCache.LoadAsync(file);

            if (!tests.TryGetValue(testName, out var test) || test == null)
            {
                var end0 = DateTimeOffset.UtcNow;
                sink.Record(hiveCaseId, caseName, false, $"test '{testName}' not found in {file} on re-parse", start, end0);
                Assert.Fail($"Test '{testName}' not found in {file} on re-parse.");
                return;
            }

            ConformanceCaseResult result;
            try
            {
                result = await EestStateTestsDriver.Instance.RunAsync(test, conformanceCase.Fork!, conformanceCase.DataIndex, conformanceCase.GasIndex, conformanceCase.ValueIndex);
            }
            catch (Exception ex)
            {
                result = ConformanceCaseResult.Fail("harnessException", $"{ex.GetType().Name}: {ex.Message}");
            }

            var end = DateTimeOffset.UtcNow;
            var details = result.Success ? "" : $"[{result.Kind}] {result.Detail}";
            sink.Record(hiveCaseId, caseName, result.Success, details, start, end);

            var diagnosis = await StateCaseDiagnosis.DiagnoseAsync(conformanceCase, test, result);
            if (diagnosis != null) details += $" (diagnosis: {diagnosis})";

            Assert.True(result.Success, details);
        }
    }
}
