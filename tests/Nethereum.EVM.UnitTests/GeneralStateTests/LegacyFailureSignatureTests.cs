using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class LegacyFailureSignatureTests
    {
        private readonly ITestOutputHelper _output;
        public LegacyFailureSignatureTests(ITestOutputHelper output) { _output = output; }

        private static string LegacyRoot(string branch)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return Path.Combine(dir.FullName, "external", "legacytests", branch, "GeneralStateTests");
                dir = dir.Parent;
            }
            return null;
        }

        private static string ProjectRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        private static string BranchFor(string fork) =>
            fork == "Frontier" || fork == "Homestead" || fork == "EIP150" || fork == "EIP158" ||
            fork == "Byzantium" || fork == "Constantinople" || fork == "ConstantinopleFix"
                ? "Constantinople" : "Cancun";

        public static IEnumerable<object[]> FailingCellsFromSweep()
        {
            var sweepDir = Path.Combine(ProjectRoot(), "tmp", "test_results", "sweep");
            int yielded = 0;
            if (Directory.Exists(sweepDir))
            {
                foreach (var csv in Directory.EnumerateFiles(sweepDir, "sweep_*_*.csv"))
                {
                    var name = Path.GetFileNameWithoutExtension(csv);
                    var rest = name.Substring("sweep_".Length);
                    var firstUnderscore = rest.IndexOf('_');
                    if (firstUnderscore < 0) continue;
                    var fork = rest.Substring(0, firstUnderscore);
                    var category = rest.Substring(firstUnderscore + 1);
                    bool hasFail = false;
                    using (var r = new StreamReader(csv))
                    {
                        string line; bool first = true;
                        while ((line = r.ReadLine()) != null)
                        {
                            if (first) { first = false; continue; }
                            if (line.IndexOf(",FAIL,", StringComparison.Ordinal) >= 0) { hasFail = true; break; }
                        }
                    }
                    if (!hasFail) continue;
                    yielded++;
                    yield return new object[] { BranchFor(fork), category, fork };
                }
            }
            if (yielded == 0)
                yield return new object[] { "__no_sweep_data__", "__no_sweep_data__", "__no_sweep_data__" };
        }

        [Theory]
        [Trait("Category", "LegacyFork-Signature-FromSweep")]
        [MemberData(nameof(FailingCellsFromSweep))]
        public Task SignatureCategoryFromSweep(string branch, string category, string fork)
        {
            if (branch == "__no_sweep_data__")
            {
                _output.WriteLine("No sweep CSVs with FAIL rows found. Run a SweepFork_* test first to populate tmp/test_results/sweep/.");
                return Task.CompletedTask;
            }
            return SignatureCategory(branch, category, fork);
        }

        [Theory]
        [Trait("Category", "LegacyFork-Signature")]
        [InlineData("Constantinople", "stWalletTest", "Constantinople")]
        [InlineData("Constantinople", "stRefundTest", "Constantinople")]
        [InlineData("Constantinople", "stExtCodeHash", "Constantinople")]
        [InlineData("Constantinople", "stCallCodes", "Constantinople")]
        [InlineData("Constantinople", "stCallCreateCallCodeTest", "Constantinople")]
        [InlineData("Constantinople", "stZeroCallsTest", "Constantinople")]
        public async Task SignatureCategory(string branch, string category, string fork)
        {
            var root = LegacyRoot(branch);
            if (root == null) { _output.WriteLine("legacytests not cloned"); return; }
            var categoryDir = Path.Combine(root, category);
            if (!Directory.Exists(categoryDir)) { _output.WriteLine($"missing: {categoryDir}"); return; }

            var t8nRunner = new GethT8nRunner();
            var sigClassifier = new PostStateSignatureClassifier();
            var entries = new List<SignatureEntry>();

            foreach (var file in Directory.GetFiles(categoryDir, "*.json"))
            {
                var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
                TestResult fileResult;
                try { fileResult = await runner.RunTestWithExecutorAsync(file, specificDataIndex: null, captureTraces: false); }
                catch { continue; }
                foreach (var r in fileResult.Results)
                {
                    if (r.Passed || r.Skipped) continue;
                    var entry = await DiffSubTestAsync(file, r.DataIndex, r.GasIndex, r.ValueIndex, fork, t8nRunner, sigClassifier);
                    entries.Add(entry);
                }
            }

            var bySig = entries.GroupBy(e => e.Signature).OrderByDescending(g => g.Count()).ToList();
            _output.WriteLine($"=== {category} @ {fork}: {entries.Count} failing sub-tests ===");
            foreach (var g in bySig) _output.WriteLine($"  {g.Key,-32} = {g.Count(),4}");
            foreach (var g in bySig)
            {
                _output.WriteLine($"-- {g.Key} --");
                foreach (var e in g.Take(3))
                {
                    _output.WriteLine($"  {e.FileName} [{e.DataIndex},{e.GasIndex},{e.ValueIndex}] : {e.Detail}");
                }
            }

            var outDir = Path.Combine(ProjectRoot(), "tmp", "test_results", "signature");
            Directory.CreateDirectory(outDir);
            var csv = Path.Combine(outDir, $"signature_{branch}_{category}_{fork}.csv");
            using (var w = new StreamWriter(csv))
            {
                w.WriteLine("Fork,FileName,DataIndex,GasIndex,ValueIndex,Signature,DiffCount,Detail");
                foreach (var e in entries)
                    w.WriteLine($"{e.Fork},{e.FileName},{e.DataIndex},{e.GasIndex},{e.ValueIndex},{e.Signature},{e.DiffCount},\"{e.Detail?.Replace("\"", "\"\"")}\"");
            }
            _output.WriteLine($"CSV: {csv}");
        }

        private async Task<SignatureEntry> DiffSubTestAsync(string file, int dataIndex, int gasIndex, int valueIndex, string fork,
            GethT8nRunner t8nRunner, PostStateSignatureClassifier sigClassifier)
        {
            var entry = new SignatureEntry
            {
                FilePath = file, FileName = Path.GetFileName(file),
                DataIndex = dataIndex, GasIndex = gasIndex, ValueIndex = valueIndex,
                Fork = fork
            };
            var runner = new GeneralStateTestRunner(_output, targetHardfork: fork);
            ExecutionStateService ourState;
            string coinbase, sender;
            try
            {
                var (state, cb, snd) = await runner.RunAndCaptureExecutionStateAsync(file, dataIndex);
                ourState = state; coinbase = cb; sender = snd;
            }
            catch (Exception ex)
            {
                entry.Signature = "RUNNER_EXCEPTION".ToString();
                entry.Detail = ex.Message;
                return entry;
            }
            if (ourState == null)
            {
                entry.Signature = "RUNNER_NO_STATE".ToString();
                return entry;
            }
            var gethResult = await t8nRunner.RunT8nAsync(file, dataIndex, gasIndex, valueIndex, fork);
            if (!gethResult.Success || gethResult.PostState == null)
            {
                entry.Signature = "GETH_FAILED".ToString();
                entry.Detail = gethResult.Error + " | tx=" + (gethResult.TxsFileContent ?? "");
                return entry;
            }
            var cmp = sigClassifier.Compare(
                ourState.AccountsState.ToDictionary(kvp => kvp.Key.ToHexLower(), kvp => kvp.Value),
                gethResult.PostState, coinbase, sender);
            entry.Signature = cmp.Signature.ToString();
            entry.DiffCount = cmp.Diffs.Count;
            entry.Detail = cmp.Summary(maxDiffs: 4);
            return entry;
        }

        private class SignatureEntry
        {
            public string FilePath, FileName, Fork, Signature, Detail;
            public int DataIndex, GasIndex, ValueIndex, DiffCount;
        }
    }
}
