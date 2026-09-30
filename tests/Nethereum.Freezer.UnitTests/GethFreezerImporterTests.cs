using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Nethereum.Freezer.UnitTests
{
    public class GethFreezerImporterTests
    {
        private static readonly string FixturesDirectory =
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "geth-ancient");

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private static void CopyFixturesInto(string destinationDirectory)
        {
            foreach (var file in Directory.GetFiles(FixturesDirectory))
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)));
        }

        [Fact]
        public void Given_UnprunedSource_When_InspectSource_Then_IsCompleteHistoryAndNoPrunedGroups()
        {
            var layout = new FreezerLayout(FixturesDirectory);
            var importer = new GethFreezerImporter();

            var readiness = importer.InspectSource(layout);

            Assert.True(readiness.IsCompleteHistory);
            Assert.Empty(readiness.PrunedGroups);
            Assert.True(readiness.Items > 0);
        }

        [Fact]
        public void Given_TailsWithNonZeroGroup_When_BuildReadiness_Then_FlagsPrunedGroupNotComplete()
        {
            var tails = new Dictionary<string, TableTail>
            {
                ["headers+hashes"] = new TableTail(0, new[] { "headers", "hashes" }),
                ["bodies+receipts"] = new TableTail(500, new[] { "bodies", "receipts" }),
                ["bals"] = new TableTail(0, new[] { "bals" }),
            };

            var readiness = GethFreezerImporter.BuildReadiness(tails, items: 12345);

            Assert.False(readiness.IsCompleteHistory);
            Assert.Equal(12345L, readiness.Items);
            var pruned = Assert.Single(readiness.PrunedGroups);
            Assert.Equal("bodies+receipts", pruned.GroupLabel);
            Assert.Equal(500L, pruned.VirtualTail);
            Assert.Contains("bodies", pruned.Members);
            Assert.Contains("receipts", pruned.Members);
        }

        [Fact]
        public void Given_AllTailsZero_When_BuildReadiness_Then_CompleteHistoryNoPrunedGroups()
        {
            var tails = new Dictionary<string, TableTail>
            {
                ["headers+hashes"] = new TableTail(0, new[] { "headers", "hashes" }),
                ["bodies+receipts"] = new TableTail(0, new[] { "bodies", "receipts" }),
                ["bals"] = new TableTail(0, new[] { "bals" }),
            };

            var readiness = GethFreezerImporter.BuildReadiness(tails, items: 999);

            Assert.True(readiness.IsCompleteHistory);
            Assert.Empty(readiness.PrunedGroups);
            Assert.Equal(999L, readiness.Items);
        }

        [Fact]
        public void Given_ImportedFreezer_When_InspectSource_Then_ValidatePasses()
        {
            var layout = new FreezerLayout(FixturesDirectory);
            var importer = new GethFreezerImporter();

            var exception = Record.Exception(() => importer.InspectSource(layout));

            Assert.Null(exception);
        }

        [Fact]
        public void Given_TornSource_When_InspectSource_Then_Throws()
        {
            var dir = CreateTempDirectory();
            try
            {
                CopyFixturesInto(dir);

                var bodiesIndexPath = Path.Combine(dir, "bodies.cidx");
                var bytes = File.ReadAllBytes(bodiesIndexPath);
                File.WriteAllBytes(bodiesIndexPath, bytes.Take(bytes.Length - 3).ToArray());

                var layout = new FreezerLayout(dir);
                var importer = new GethFreezerImporter();

                Assert.Throws<FreezerValidationException>(() => importer.InspectSource(layout));
            }
            finally
            {
                DeleteDirectory(dir);
            }
        }
    }
}
