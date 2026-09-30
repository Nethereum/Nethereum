using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Nethereum.MainnetChain.UnitTests
{
    public class NothingDecidesItsOwnForkTests
    {
        private static readonly Regex DecidesAFork = new Regex(
            @"new\s+FixedChainActivations\s*\(|AssumingUndescribedChainsRun\s*\(|DefaultHardforkConfigs\.(Frontier|Homestead|TangerineWhistle|SpuriousDragon|Byzantium|Constantinople|Petersburg|Istanbul|Berlin|London|Paris|Shanghai|Cancun|Prague|Osaka|Amsterdam)|HardforkConfig\.(Cancun|Prague|Osaka|Amsterdam)\b|\bHardfork\s*=\s*""",
            RegexOptions.Compiled);

        private static readonly Dictionary<string, string> StillDecidingForThemselves =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nethereum.EVM.Precompiles/DefaultChainForkResolver.cs"] =
                    "the one place a default fork for an undescribed chain is named; every simulator now asks it via .Default instead of naming a fork itself",
                ["Nethereum.MainnetChain/Hosting/MainnetChainNodeFactory.cs"] =
                    "pins the follower TransactionProcessor with Hardfork = \"cancun\"; feeds a dead field, verdict owed (G14)",
                ["Nethereum.EVM.Zisk/Zisk/ZiskBinaryWitness.cs"] =
                    "Zisk witness codec; a separate lane",
            };

        private static string SourceRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var src = Path.Combine(directory.FullName, "src");
                if (Directory.Exists(Path.Combine(src, "Nethereum.CoreChain"))) return src;
                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"No ancestor of {AppContext.BaseDirectory} holds src/Nethereum.CoreChain, so the source tree " +
                "cannot be scanned and this guard would pass by finding nothing.");
        }

        private static IEnumerable<string> SitesDecidingAFork()
        {
            var root = SourceRoot();

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = file.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                if (!DecidesAFork.IsMatch(File.ReadAllText(file))) continue;

                yield return relative;
            }
        }

        [Fact]
        public void Given_TheSourceTree_When_ScannedForComponentsDecidingTheirOwnFork_Then_OnlyTheKnownOnesDoSo()
        {
            var unlisted = SitesDecidingAFork()
                .Where(site => !StillDecidingForThemselves.ContainsKey(site))
                .OrderBy(site => site, StringComparer.Ordinal)
                .ToList();

            Assert.True(unlisted.Count == 0,
                $"{string.Join(", ", unlisted)} decides which fork it runs instead of asking the chain's " +
                "configuration. CFT-ONEPLACE: that rule is expressed in ChainNodeConfig.Chain. If this site " +
                "genuinely has to state a fork, add it to StillDecidingForThemselves with what it costs.");
        }

        [Fact]
        public void Given_TheListOfKnownSites_When_Checked_Then_EachOneStillExists()
        {
            var found = SitesDecidingAFork().ToHashSet(StringComparer.OrdinalIgnoreCase);

            var gone = StillDecidingForThemselves.Keys
                .Where(site => !found.Contains(site))
                .OrderBy(site => site, StringComparer.Ordinal)
                .ToList();

            Assert.True(gone.Count == 0,
                $"{string.Join(", ", gone)} no longer decides its own fork. Delete its line: a list that " +
                "outlives what it describes stops being a count of the work left.");
        }
    }
}
