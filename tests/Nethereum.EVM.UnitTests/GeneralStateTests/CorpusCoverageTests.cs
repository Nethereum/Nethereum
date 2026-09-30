using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class CorpusCoverageTests
    {
        private static readonly string[] ForksThePinIsExpectedToShip = { "amsterdam" };

        private static string FixturesRoot(string suite)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "external")))
                dir = dir.Parent;

            Assert.True(dir != null, "Could not find the repository root from the test working directory.");
            return Path.Combine(dir.FullName, "external", "execution-spec-tests", "fixtures", suite);
        }

        [Theory]
        [InlineData("state_tests")]
        [InlineData("blockchain_tests")]
        public void Given_ThePinnedCorpus_When_ItsForksAreListed_Then_TheyAreExactlyTheOnesTheSuitesExpect(string suite)
        {
            var root = FixturesRoot(suite);
            Assert.True(Directory.Exists(root),
                $"The pinned corpus is missing entirely at {root}. See external/README.md for the download and " +
                "note that the archive nests an extra 'for_amsterdam/' level that must be stripped.");

            var present = new DirectoryInfo(root).GetDirectories()
                .Select(d => d.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(ForksThePinIsExpectedToShip, present);
        }

        [Fact]
        public void Given_TheStaticSpecTestTree_When_ItsPresenceIsChecked_Then_ItMatchesWhatThePinShips()
        {
            var staticRoot = Path.Combine(FixturesRoot("state_tests"), "static", "state_tests");

            Assert.False(Directory.Exists(staticRoot),
                $"The static spec-test tree is present at {staticRoot}, which the current pin " +
                "does not ship. If it was added deliberately, update this expectation — the " +
                "stPreCompiledContracts categories can now run and should be verified.");
        }

        [Fact]
        public void Given_AForkThePinShips_When_ItsDirectoryIsRead_Then_ItHasCategoriesRatherThanBeingEmpty()
        {
            foreach (var suite in new[] { "state_tests", "blockchain_tests" })
            {
                var forkPath = Path.Combine(FixturesRoot(suite), "amsterdam");
                Assert.True(Directory.Exists(forkPath), $"{suite}/amsterdam is absent from the pinned corpus.");

                var categories = new DirectoryInfo(forkPath).GetDirectories();
                Assert.True(categories.Length > 0,
                    $"{suite}/amsterdam exists but holds no categories — the archive's extra " +
                    "'for_amsterdam/' level was probably not stripped. Every Amsterdam test would " +
                    "otherwise pass on an empty corpus.");
            }
        }
    }
}
