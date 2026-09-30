using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.EVM.UnitTests.Execution
{
    public class BlockExecutorArmsAgreeTests
    {
        private const string TargetFile = "src/Nethereum.EVM.Core/Execution/BlockExecutor.cs";

        private const string DeclaredBlockAccessListCheckUnit =
            "DeclaredBlockAccessListCheck = BlockAccessListStructureRule " +
            ".FindViolation(block.DeclaredBlockAccessList, block.Transactions.Count),";

        [NethereumDocExample(DocSection.EvmSimulator, "sync-and-async-arms", "The sync and async BlockExecutor arms are diffed statement by statement and must agree", Order = 1)]
        [Fact]
        public void Given_TheTwoBlockExecutorArms_When_NormalisedAndDiffed_Then_TheOnlySyncOnlyStatementIsTheDeclaredBlockAccessListCheck()
        {
            var arms = ReadArms();
            var difference = Diff(arms.Sync, arms.Async, naiveSemicolonJoin: false);

            var only = Assert.Single(difference.SyncOnly);
            Assert.Equal(DeclaredBlockAccessListCheckUnit, only);
            Assert.Empty(difference.AsyncOnly);
            Assert.True(IsTheOneExpectedAsymmetry(difference));
        }

        [Fact]
        public void Given_AStrayStatementInTheSyncArmOnly_When_Diffed_Then_ItSurfacesAsAnExtraSyncOnlyStatement()
        {
            var arms = ReadArms();
            var mutated = ReplaceOnce(arms.Sync,
                "long cumulativeGasUsed = 0;",
                "long cumulativeGasUsed = 0;\n            var strayProbe = 1;");

            var difference = Diff(mutated, arms.Async, naiveSemicolonJoin: false);

            Assert.Contains("var strayProbe = 1;", difference.SyncOnly);
            Assert.False(IsTheOneExpectedAsymmetry(difference));
        }

        [Fact]
        public void Given_TheDeclaredBlockAccessListCheckDeleted_When_Diffed_Then_TheNowIdenticalArmsAreRejected()
        {
            var arms = ReadArms();
            var mutated = DeleteDeclaredBlockAccessListCheck(arms.Sync);

            var difference = Diff(mutated, arms.Async, naiveSemicolonJoin: false);

            Assert.Empty(difference.SyncOnly);
            Assert.Empty(difference.AsyncOnly);
            Assert.False(IsTheOneExpectedAsymmetry(difference));
        }

        [Fact]
        public void Given_ADuplicatedMemberInTheAsyncInitializerOnly_When_Diffed_Then_TheAsyncOnlySetIsNoLongerEmpty()
        {
            var arms = ReadArms();
            var mutated = ReplaceOnce(arms.Async,
                "                Receipts = receipts,",
                "                Receipts = receipts,\n                Receipts = receipts,");

            var difference = Diff(arms.Sync, mutated, naiveSemicolonJoin: false);

            Assert.Contains("Receipts = receipts,", difference.AsyncOnly);
            Assert.False(IsTheOneExpectedAsymmetry(difference));
        }

        [Fact]
        public void Given_TheSameDuplicatedMember_When_JoinedNaivelyAtSemicolons_Then_ItIsAbsorbedAndNoNewDifferenceAppears()
        {
            var arms = ReadArms();
            var mutated = ReplaceOnce(arms.Async,
                "                Receipts = receipts,",
                "                Receipts = receipts,\n                Receipts = receipts,");

            var clean = Diff(arms.Sync, arms.Async, naiveSemicolonJoin: true);
            var dirty = Diff(arms.Sync, mutated, naiveSemicolonJoin: true);

            Assert.Equal(clean.SyncOnly.Count, dirty.SyncOnly.Count);
            Assert.Equal(clean.AsyncOnly.Count, dirty.AsyncOnly.Count);
        }

        [Fact]
        public void Given_AStatementMovedWithinOneArmOnly_When_TheOrderedUnitsAreCompared_Then_TheArmsNoLongerAgree()
        {
            var arms = ReadArms();

            Assert.Equal(SyncUnitsWithoutTheExpectedAsymmetry(arms.Sync), AsyncUnits(arms.Async));

            var reordered = MoveForcePostStateRootPastItsGuard(arms.Async);
            Assert.NotEqual(SyncUnitsWithoutTheExpectedAsymmetry(arms.Sync), AsyncUnits(reordered));
        }

        private static List<string> AsyncUnits(string asyncRegion) =>
            Units(Normalise(asyncRegion), naiveSemicolonJoin: false);

        private static List<string> SyncUnitsWithoutTheExpectedAsymmetry(string syncRegion)
        {
            var units = Units(Normalise(syncRegion), naiveSemicolonJoin: false);
            var at = units.IndexOf(DeclaredBlockAccessListCheckUnit);
            Assert.True(at >= 0, "the expected sync-only unit was not found in the sync sequence");
            units.RemoveAt(at);
            return units;
        }

        private static string MoveForcePostStateRootPastItsGuard(string asyncRegion)
        {
            const string forcing = "ForceComputePostStateRootWhenProducingCommitments(block);";
            var at = asyncRegion.IndexOf(forcing, StringComparison.Ordinal);
            Assert.True(at >= 0, "the forcing call was not found in the async arm");
            var without = asyncRegion.Remove(at, forcing.Length);
            var anchor = without.IndexOf("var accounts", StringComparison.Ordinal);
            Assert.True(anchor >= 0, "no anchor to move the forcing to");
            return without.Insert(anchor, forcing + "\n            ");
        }

        private static bool IsTheOneExpectedAsymmetry(ArmDifference difference)
        {
            return difference.SyncOnly.Count == 1
                && difference.SyncOnly[0] == DeclaredBlockAccessListCheckUnit
                && difference.AsyncOnly.Count == 0;
        }

        private static string ReplaceOnce(string text, string oldValue, string newValue)
        {
            Assert.Equal(1, CountOccurrences(text, oldValue));
            return text.Replace(oldValue, newValue);
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var at = text.IndexOf(value, StringComparison.Ordinal);
            while (at >= 0)
            {
                count++;
                at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal);
            }
            return count;
        }

        private static string DeleteDeclaredBlockAccessListCheck(string region)
        {
            var kept = region.Split('\n')
                .Where(line => !line.Contains("DeclaredBlockAccessListCheck = BlockAccessListStructureRule")
                            && !line.Contains(".FindViolation(block.DeclaredBlockAccessList"))
                .ToList();
            Assert.Equal(2, region.Split('\n').Length - kept.Count);
            return string.Join("\n", kept);
        }

        private sealed class Arms
        {
            public string Sync { get; set; }
            public string Async { get; set; }
        }

        private sealed class ArmDifference
        {
            public List<string> SyncOnly { get; set; }
            public List<string> AsyncOnly { get; set; }
        }

        private static Arms ReadArms()
        {
            var text = File.ReadAllText(LocateSource());
            var arms = SplitRegions(text);
            Assert.NotNull(arms.Sync);
            Assert.NotNull(arms.Async);
            return arms;
        }

        private static string LocateSource()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, TargetFile.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Could not locate " + TargetFile + " above " + AppContext.BaseDirectory);
        }

        private static readonly Regex ConditionalDirective =
            new Regex(@"^\s*#\s*(if|endif)\b(.*)$", RegexOptions.Compiled);

        private static Arms SplitRegions(string text)
        {
            var lines = text.Split('\n');
            var open = new Stack<KeyValuePair<int, string>>();
            var arms = new Arms();
            for (var i = 0; i < lines.Length; i++)
            {
                var match = ConditionalDirective.Match(lines[i]);
                if (!match.Success) continue;
                if (match.Groups[1].Value == "if")
                {
                    open.Push(new KeyValuePair<int, string>(i, match.Groups[2].Value.Trim()));
                    continue;
                }
                if (open.Count == 0) continue;
                var start = open.Pop();
                var body = string.Join("\n", lines.Skip(start.Key + 1).Take(i - start.Key - 1));
                if (start.Value == "EVM_SYNC" && body.Contains("BlockExecutionResult Execute(")) arms.Sync = body;
                if (start.Value == "!EVM_SYNC" && body.Contains("ExecuteAsync(")) arms.Async = body;
            }
            return arms;
        }

        private static ArmDifference Diff(string syncRegion, string asyncRegion, bool naiveSemicolonJoin)
        {
            var sync = Tally(Units(Normalise(syncRegion), naiveSemicolonJoin));
            var async = Tally(Units(Normalise(asyncRegion), naiveSemicolonJoin));
            return new ArmDifference
            {
                SyncOnly = Excess(sync, async),
                AsyncOnly = Excess(async, sync)
            };
        }

        private static Dictionary<string, int> Tally(List<string> units)
        {
            var tally = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var unit in units)
            {
                tally.TryGetValue(unit, out var count);
                tally[unit] = count + 1;
            }
            return tally;
        }

        private static List<string> Excess(Dictionary<string, int> left, Dictionary<string, int> right)
        {
            var excess = new List<string>();
            foreach (var entry in left)
            {
                right.TryGetValue(entry.Key, out var seen);
                for (var i = seen; i < entry.Value; i++) excess.Add(entry.Key);
            }
            return excess;
        }

        private static readonly Regex AnyDirective =
            new Regex(@"^\s*#\s*(if|else|elif|endif|region|endregion)\b", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRun = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex AsyncTaskOfT = new Regex(@"async Task<([^>]*)> ", RegexOptions.Compiled);
        private static readonly Regex AsyncSuffixedCall =
            new Regex(@"([A-Za-z_][A-Za-z0-9_]*)Async\s*\(", RegexOptions.Compiled);

        private static List<string> Normalise(string region)
        {
            var normalised = new List<string>();
            foreach (var raw in StripComments(region).Split('\n'))
            {
                if (AnyDirective.IsMatch(raw)) continue;
                var line = WhitespaceRun.Replace(raw, " ").Trim();
                if (line.Length == 0) continue;
                line = line.Replace("await ", string.Empty);
                line = AsyncTaskOfT.Replace(line, "$1 ");
                line = line.Replace("async Task ", "void ");
                line = AsyncSuffixedCall.Replace(line, "$1(");
                normalised.Add(line);
            }
            return normalised;
        }

        private static string StripComments(string text)
        {
            var kept = new StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '@' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i = CopyVerbatimString(text, i, kept);
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    i = CopyQuoted(text, i, c, kept);
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                    {
                        if (text[i] == '\n') kept.Append('\n');
                        i++;
                    }
                    i += 2;
                    continue;
                }
                kept.Append(c);
                i++;
            }
            return kept.ToString();
        }

        private static int CopyVerbatimString(string text, int i, StringBuilder kept)
        {
            kept.Append(text[i]).Append(text[i + 1]);
            i += 2;
            while (i < text.Length)
            {
                if (text[i] == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        kept.Append('"').Append('"');
                        i += 2;
                        continue;
                    }
                    kept.Append('"');
                    return i + 1;
                }
                kept.Append(text[i]);
                i++;
            }
            return i;
        }

        private static int CopyQuoted(string text, int i, char quote, StringBuilder kept)
        {
            kept.Append(text[i]);
            i++;
            while (i < text.Length)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    kept.Append(text[i]).Append(text[i + 1]);
                    i += 2;
                    continue;
                }
                kept.Append(text[i]);
                if (text[i] == quote) return i + 1;
                i++;
            }
            return i;
        }

        private static string CodeOnly(string line)
        {
            var kept = new StringBuilder(line.Length);
            var i = 0;
            while (i < line.Length)
            {
                var c = line[i];
                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i += 2;
                    while (i < line.Length)
                    {
                        if (line[i] == '"')
                        {
                            if (i + 1 < line.Length && line[i + 1] == '"') { i += 2; continue; }
                            i++;
                            break;
                        }
                        i++;
                    }
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    var quote = c;
                    i++;
                    while (i < line.Length)
                    {
                        if (line[i] == '\\') { i += 2; continue; }
                        if (line[i] == quote) { i++; break; }
                        i++;
                    }
                    continue;
                }
                kept.Append(c);
                i++;
            }
            return kept.ToString();
        }

        private static readonly char[] UnitTerminators = { ';', '{', '}', ',' };
        private static readonly string[] ContinuationOperators = { "&&", "||", "+", "-", "*", "/", "?", ":", "=" };

        private static bool IsIncomplete(string unit)
        {
            var code = CodeOnly(unit);
            if (Count(code, '(') != Count(code, ')')) return true;
            if (Count(code, '[') != Count(code, ']')) return true;
            var trimmed = code.TrimEnd();
            if (trimmed.Length == 0) return true;
            foreach (var op in ContinuationOperators)
                if (trimmed.EndsWith(op, StringComparison.Ordinal)) return true;
            return Array.IndexOf(UnitTerminators, trimmed[trimmed.Length - 1]) < 0;
        }

        private static int Count(string text, char c)
        {
            var n = 0;
            foreach (var ch in text) if (ch == c) n++;
            return n;
        }

        private static List<string> Units(List<string> lines, bool naiveSemicolonJoin)
        {
            var units = new List<string>();
            string current = null;
            var currentDepth = 0;
            var depth = 0;
            foreach (var line in lines)
            {
                var code = CodeOnly(line);
                var startDepth = depth;
                if (current == null)
                {
                    current = line;
                    currentDepth = startDepth;
                }
                else
                {
                    var joins = naiveSemicolonJoin
                        ? !CodeOnly(current).TrimEnd().EndsWith(";", StringComparison.Ordinal)
                        : IsIncomplete(current) && startDepth == currentDepth;
                    if (joins)
                    {
                        current = current + " " + line;
                    }
                    else
                    {
                        units.Add(current);
                        current = line;
                        currentDepth = startDepth;
                    }
                }
                depth += Count(code, '{') - Count(code, '}');
            }
            if (current != null) units.Add(current);
            return units;
        }
    }
}
