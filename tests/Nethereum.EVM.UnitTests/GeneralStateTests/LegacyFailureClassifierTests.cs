using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Driver tests for <see cref="LegacyFailureClassifier"/>. Each Theory
    /// row picks a (legacytests branch, category, fork) tuple, classifies
    /// every failing sub-test in that category, prints a per-class summary,
    /// and writes a CSV under <c>tmp/test_results/classifier/</c>.
    /// </summary>
    public class LegacyFailureClassifierTests
    {
        private readonly ITestOutputHelper _output;
        public LegacyFailureClassifierTests(ITestOutputHelper output) { _output = output; }

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
        /// Classify every failing sub-test in a given category at a given
        /// fork. The summary breaks down EVM_OPCODE_DIFF vs
        /// POST_EVM_DIVERGENCE counts so we can see whether the failures
        /// cluster as opcode-level bugs or finalisation rules. CSV with
        /// per-fixture detail is written for offline analysis.
        /// </summary>
        [Theory]
        [Trait("Category", "LegacyFork-Classify")]
        [InlineData("Constantinople", "stRefundTest", "Constantinople")]
        [InlineData("Constantinople", "stZeroCallsTest", "Constantinople")]
        [InlineData("Constantinople", "stWalletTest", "Constantinople")]
        [InlineData("Constantinople", "stExtCodeHash", "Constantinople")]
        public async Task ClassifyCategory(string branch, string category, string fork)
        {
            var root = LegacyRoot(branch);
            if (root == null) { _output.WriteLine("legacytests not cloned; skipping."); return; }
            var categoryDir = Path.Combine(root, category);
            if (!Directory.Exists(categoryDir)) { _output.WriteLine($"category not present: {categoryDir}"); return; }

            var classifier = new LegacyFailureClassifier(_output);
            var classifications = await classifier.ClassifyCategoryAsync(categoryDir, fork);

            // Summary by class
            var byClass = classifications.GroupBy(c => c.Class).OrderByDescending(g => g.Count()).ToList();
            _output.WriteLine($"=== {category} @ {fork}: {classifications.Count} failing sub-tests ===");
            foreach (var g in byClass)
            {
                _output.WriteLine($"  {g.Key,-25} = {g.Count(),4}");
            }

            // Per-class detail (top examples)
            foreach (var g in byClass)
            {
                _output.WriteLine($"-- {g.Key} examples --");
                foreach (var c in g.Take(5))
                {
                    _output.WriteLine($"   {c.FileName,-50} [{c.DataIndex},{c.GasIndex},{c.ValueIndex}] {c.Detail}");
                }
            }

            // Top EVM_OPCODE_DIFF opcodes (when present)
            var opcodeDiffs = classifications.Where(c => c.Class == FailureClass.EVM_OPCODE_DIFF && !string.IsNullOrEmpty(c.FirstDiffOpcode))
                .GroupBy(c => c.FirstDiffOpcode + " / " + c.FirstDiffField)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();
            if (opcodeDiffs.Count > 0)
            {
                _output.WriteLine("-- top EVM_OPCODE_DIFF (opcode/field) --");
                foreach (var g in opcodeDiffs)
                {
                    _output.WriteLine($"   {g.Count(),3}x  {g.Key}");
                }
            }

            // CSV out
            var outDir = Path.Combine(ProjectRoot(), "tmp", "test_results", "classifier");
            Directory.CreateDirectory(outDir);
            var csvPath = Path.Combine(outDir, $"classify_{branch}_{category}_{fork}.csv");
            using (var w = new StreamWriter(csvPath))
            {
                w.WriteLine(FailureClassification.CsvHeader());
                foreach (var c in classifications) w.WriteLine(c.ToCsv());
            }
            _output.WriteLine($"CSV: {csvPath}");
        }
    }
}
