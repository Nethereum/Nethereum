using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class GasRuleConditionalCompilationGuardTests
    {
        private static readonly string[] IoSeamFilesWhereTheDirectiveBelongs =
        {
            "IOpcodeGasCostAsync.cs",
            "SstoreSlotResolvingGasCost.cs",
            "SelfDestructBeneficiaryResolvingGasCost.cs",
            "CallTargetResolvingGasCost.cs"
        };

        private static readonly string[] HaltSeamFilesWhereTheDirectiveBelongs =
        {
            "StateGasMeter.cs"
        };

        private static readonly string[] RulesAwaitingTheirResolver =
        {
            "CallGasCosts.cs"
        };

        [Fact]
        public void Given_AGasRuleSource_When_ItIsScanned_Then_ItCarriesNoConditionalCompilationDirective()
        {
            var offenders = RuleSources()
                .Where(source => !IsAllowedToCarryTheDirective(source.Name))
                .Select(source => new { source.Name, Directives = DirectiveLines(File.ReadAllText(source.Path)) })
                .Where(scanned => scanned.Directives.Count > 0)
                .Select(scanned => scanned.Name + ": " + string.Join(", ", scanned.Directives))
                .ToList();

            Assert.True(offenders.Count == 0,
                "Conditional compilation inside a gas rule:" + Environment.NewLine +
                string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void Given_AFileAllowedToCarryTheDirective_When_TheGuardRuns_Then_ItStillCarriesOne()
        {
            var sourcesByName = RuleSources().ToDictionary(source => source.Name, source => source.Path);

            var stale = IoSeamFilesWhereTheDirectiveBelongs
                .Concat(HaltSeamFilesWhereTheDirectiveBelongs)
                .Concat(RulesAwaitingTheirResolver)
                .Where(name => !sourcesByName.ContainsKey(name)
                            || DirectiveLines(File.ReadAllText(sourcesByName[name])).Count == 0)
                .ToList();

            Assert.True(stale.Count == 0,
                "Listed as carrying a conditional directive, but no longer does — remove from the list:" +
                Environment.NewLine + string.Join(Environment.NewLine, stale));
        }

        [Fact]
        public void Given_ASourceCarryingAConditionalDirective_When_TheScannerReadsIt_Then_ItIsReported()
        {
            const string twinnedRule =
                "public long GetGasCost(Program program)\n" +
                "{\n" +
                "#if EVM_SYNC\n" +
                "    return 1;\n" +
                "#else\n" +
                "    return 2;\n" +
                "#endif\n" +
                "}\n";

            Assert.Equal(new List<int> { 3, 5, 7 }, DirectiveLines(twinnedRule));
        }

        [Fact]
        public void Given_ASourceWithoutAConditionalDirective_When_TheScannerReadsIt_Then_NothingIsReported()
        {
            const string singleBody =
                "public long GetGasCost(Program program, SstoreSlotState slot)\n" +
                "{\n" +
                "    return AccessComponent(slot) + WriteComponent(slot);\n" +
                "}\n";

            Assert.Empty(DirectiveLines(singleBody));
        }

        [Fact]
        public void Given_TheGuard_When_ItLocatesTheRuleSources_Then_TheFolderIsFoundAndHoldsTheRuleClasses()
        {
            var names = RuleSources().Select(source => source.Name).ToList();

            Assert.Contains("IOpcodeGasCost.cs", names);
            Assert.Contains("FixedGasCost.cs", names);
            Assert.Contains("IAccessAccountRule.cs", names);
            Assert.Contains("Eip7976CalldataFloorRule.cs", names);
            Assert.Contains("StateGasMeter.cs", names);
        }

        private const int WidestSeamInDistinctStatements = 3;

        [Fact]
        public void Given_ATwoArmedBlockAtASeamThatHasItsResolver_When_ItIsMeasured_Then_ItIsNoWiderThanTheSeamNeeds()
        {
            var offenders = RuleSources()
                .Where(source => IsAllowedToCarryTheDirective(source.Name)
                              && !RulesAwaitingTheirResolver.Contains(source.Name))
                .SelectMany(source => TwoArmedBlocks(File.ReadAllText(source.Path))
                    .Where(block => block.Statements.Count > WidestSeamInDistinctStatements)
                    .Select(block => source.Name + ": " + block.Line + " - " +
                                     block.Statements.Count + " distinct statements"))
                .ToList();

            Assert.True(offenders.Count == 0,
                "A conditional block wide enough to be a body written once per engine:" +
                Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        public void Given_ABodyWrittenOncePerEngine_When_TheArmsAreMeasured_Then_EachStatementIsCountedOnce()
        {
            const string twinnedBody =
                "#if EVM_SYNC\n" +
                "        public long GetGasCost(Program program)\n" +
                "        {\n" +
                "            var contract = program.ProgramContext.AddressContract;\n" +
                "            var cold = MarkWarmAndReportColdAccess(account, key);\n" +
                "            var slot = state.GetFromStorage(contract, key);\n" +
                "            return _rule.GetGasCost(Resolve(slot, cold));\n" +
                "        }\n" +
                "#else\n" +
                "        public async Task<long> GetGasCostAsync(Program program)\n" +
                "        {\n" +
                "            var contract = program.ProgramContext.AddressContract;\n" +
                "            var cold = MarkWarmAndReportColdAccess(account, key);\n" +
                "            var slot = await state.GetFromStorageAsync(contract, key);\n" +
                "            return _rule.GetGasCost(Resolve(slot, cold));\n" +
                "        }\n" +
                "#endif\n";

            var block = Assert.Single(TwoArmedBlocks(twinnedBody));

            Assert.Equal(5, block.Statements.Count);
            Assert.True(block.Statements.Count > WidestSeamInDistinctStatements);
        }

        [Fact]
        public void Given_AnArmHoldingWhatTheOtherDoesNot_When_TheArmsAreMeasured_Then_ItCountsOnItsOwn()
        {
            const string divergentArm =
                "#if EVM_SYNC\n" +
                "            var empty = state.IsAccountEmpty(to);\n" +
                "            program.MarkAddressAsWarm(toBytes);\n" +
                "#else\n" +
                "            var empty = await state.IsAccountEmptyAsync(to);\n" +
                "#endif\n";

            var block = Assert.Single(TwoArmedBlocks(divergentArm));

            Assert.Equal(2, block.Statements.Count);
        }

        private static bool IsAllowedToCarryTheDirective(string fileName) =>
            IoSeamFilesWhereTheDirectiveBelongs.Contains(fileName) ||
            HaltSeamFilesWhereTheDirectiveBelongs.Contains(fileName) ||
            RulesAwaitingTheirResolver.Contains(fileName);

        private sealed class ConditionalBlock
        {
            public int Line;
            public int Arms = 1;
            public readonly HashSet<string> Statements = new HashSet<string>();
        }

        private static List<ConditionalBlock> TwoArmedBlocks(string source)
        {
            var open = new Stack<ConditionalBlock>();
            var closed = new List<ConditionalBlock>();
            var lines = source.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("#if"))
                {
                    open.Push(new ConditionalBlock { Line = i + 1 });
                }
                else if (trimmed.StartsWith("#else") || trimmed.StartsWith("#elif"))
                {
                    if (open.Count > 0) open.Peek().Arms++;
                }
                else if (trimmed.StartsWith("#endif"))
                {
                    if (open.Count > 0) closed.Add(open.Pop());
                }
                else if (open.Count > 0)
                {
                    var statement = NormalisedStatement(lines[i]);
                    if (statement.Length > 0) open.Peek().Statements.Add(statement);
                }
            }

            return closed.Where(block => block.Arms > 1).ToList();
        }

        private static string NormalisedStatement(string line)
        {
            var statement = Whitespace.Replace(CommentTail.Replace(line, string.Empty), string.Empty)
                .Replace(".ConfigureAwait(false)", string.Empty)
                .Replace("await", string.Empty)
                .Replace("async", string.Empty);

            string previous = null;
            while (previous != statement)
            {
                previous = statement;
                statement = TaskOfSomething.Replace(statement, "$1");
            }

            statement = statement.Replace("Async", string.Empty);
            return statement.Trim('{', '}', ';').Length == 0 ? string.Empty : statement;
        }

        private static readonly Regex CommentTail = new Regex(@"//.*$");
        private static readonly Regex Whitespace = new Regex(@"\s+");
        private static readonly Regex TaskOfSomething = new Regex(@"(?:Value)?Task<([^<>]*)>");

        private static List<int> DirectiveLines(string source)
        {
            var lines = source.Split('\n');
            var found = new List<int>();
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("#if") || trimmed.StartsWith("#else") ||
                    trimmed.StartsWith("#elif") || trimmed.StartsWith("#endif"))
                {
                    found.Add(i + 1);
                }
            }
            return found;
        }

        private static IEnumerable<(string Name, string Path)> RuleSources()
        {
            var folder = Path.Combine(RepositoryRoot(), "src", "Nethereum.EVM.Core", "Gas");
            Assert.True(Directory.Exists(folder), "Gas rule sources not found at " + folder);

            return Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Select(path => (Path.GetFileName(path), path));
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(directory.FullName, "Nethereum.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Repository root not found above " + AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
