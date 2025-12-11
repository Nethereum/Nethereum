using System.IO;
using System.Reflection;
using Xunit;

namespace Nethereum.Ssz.Tests
{
    public class SszVectorManifestTests
    {
        [Fact]
        public void VectorDirectoryExists()
        {
            var directory = Path.Combine(GetProjectDirectory(), "..", "LightClientVectors", "ssz");
            Assert.True(Directory.Exists(directory), "SSZ vector folder should exist for upcoming fixtures.");
        }

        [Fact]
        public void VectorReadme_ReferencesConsensusSpecTests()
        {
            var readme = Path.Combine(GetProjectDirectory(), "..", "LightClientVectors", "README.md");
            Assert.True(File.Exists(readme));
            Assert.Contains("consensus-spec-tests", File.ReadAllText(readme));
        }

        private static string GetProjectDirectory()
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var binDirectory = Path.GetDirectoryName(assemblyPath)!;
            return Path.GetFullPath(Path.Combine(binDirectory, "..", "..", ".."));
        }
    }
}
