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
    /// <summary>
    /// Second-level classifier: for every failing sub-test in a category,
    /// run geth t8n to capture the authoritative post-state alloc, diff
    /// it against our impl's <see cref="ExecutionStateService"/>, and
    /// label the divergence with a <see cref="PostStateSignature"/>.
    ///
    /// The output CSV groups failures by signature so a category that
    /// looked like "13 unknown POST_EVM_DIVERGENCE" becomes
    /// "13 × RECIPIENT_OF_SELFDESTRUCT_BALANCE_DIFF on address 0xec0e71ad"
    /// — i.e. one rule, one fix. Run via:
    ///   dotnet test --filter "Category=LegacyFork-Signature"
    /// </summary>
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

        /// <summary>
        /// Resolves the legacytests branch a fork lives under. Pre-Constantinople
        /// forks (incl. ConstantinopleFix) ship in the Constantinople tree;
        /// Istanbul→Cancun ship in the Cancun tree. Drives both Theory rows
        /// and the MemberData generator below.
        /// </summary>
        private static string BranchFor(string fork) =>
            fork == "Frontier" || fork == "Homestead" || fork == "EIP150" || fork == "EIP158" ||
            fork == "Byzantium" || fork == "Constantinople" || fork == "ConstantinopleFix"
                ? "Constantinople" : "Cancun";

        /// <summary>
        /// Reads every persisted sweep CSV at tmp/test_results/sweep/
        /// (produced by FrontierLegacyTests.SweepFork_*) and yields one
        /// signature-classifier cell per (fork, category) cell that contains
        /// at least one Status=FAIL row. Skips Cancun-tree files for forks
        /// already covered by the older legacytests/Constantinople branch.
        /// If no CSVs exist (sweep not yet run), yields nothing so the
        /// Theory becomes a no-op rather than failing at discovery.
        /// </summary>
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
            // xUnit hard-fails a Theory with zero MemberData rows. If no sweep
            // CSVs have FAIL rows (clean state or sweep not yet run), yield a
            // sentinel row so the Theory at least produces one no-op cell.
            if (yielded == 0)
                yield return new object[] { "__no_sweep_data__", "__no_sweep_data__", "__no_sweep_data__" };
        }

        /// <summary>
        /// Matrix-wide signature classifier: one cell per failing (fork, category)
        /// discovered in the persisted sweep CSVs. Re-runs each failing sub-test
        /// against geth t8n, diffs the post-state, and writes
        /// signature_{branch}_{category}_{fork}.csv with one POST_EVM signature
        /// per row. Closes the loop "X sub-tests fail" → "X sub-tests fail with
        /// signature Y on address Z" so each cluster maps to a per-fork strategy
        /// fix without further re-runs.
        /// </summary>
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

            // Summary by signature.
            var bySig = entries.GroupBy(e => e.Signature).OrderByDescending(g => g.Count()).ToList();
            _output.WriteLine($"=== {category} @ {fork}: {entries.Count} failing sub-tests ===");
            foreach (var g in bySig) _output.WriteLine($"  {g.Key,-32} = {g.Count(),4}");
            // Top examples per signature.
            foreach (var g in bySig)
            {
                _output.WriteLine($"-- {g.Key} --");
                foreach (var e in g.Take(3))
                {
                    _output.WriteLine($"  {e.FileName} [{e.DataIndex},{e.GasIndex},{e.ValueIndex}] : {e.Detail}");
                }
            }

            // CSV
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
            // Run our impl to get the ExecutionStateService at end-of-tx.
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
            // Run geth t8n.
            var gethResult = await t8nRunner.RunT8nAsync(file, dataIndex, gasIndex, valueIndex, fork);
            if (!gethResult.Success || gethResult.PostState == null)
            {
                entry.Signature = "GETH_FAILED".ToString();
                entry.Detail = gethResult.Error + " | tx=" + (gethResult.TxsFileContent ?? "");
                return entry;
            }
            // Diff.
            var cmp = sigClassifier.Compare(ourState.AccountsState, gethResult.PostState, coinbase, sender);
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
